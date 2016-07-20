﻿// Copyright (c) 2011 AlphaSierraPapa for the SharpDevelop Team
// 
// Permission is hereby granted, free of charge, to any person obtaining a copy of this
// software and associated documentation files (the "Software"), to deal in the Software
// without restriction, including without limitation the rights to use, copy, modify, merge,
// publish, distribute, sublicense, and/or sell copies of the Software, and to permit persons
// to whom the Software is furnished to do so, subject to the following conditions:
// 
// The above copyright notice and this permission notice shall be included in all copies or
// substantial portions of the Software.
// 
// THE SOFTWARE IS PROVIDED "AS IS", WITHOUT WARRANTY OF ANY KIND, EXPRESS OR IMPLIED,
// INCLUDING BUT NOT LIMITED TO THE WARRANTIES OF MERCHANTABILITY, FITNESS FOR A PARTICULAR
// PURPOSE AND NONINFRINGEMENT. IN NO EVENT SHALL THE AUTHORS OR COPYRIGHT HOLDERS BE LIABLE
// FOR ANY CLAIM, DAMAGES OR OTHER LIABILITY, WHETHER IN AN ACTION OF CONTRACT, TORT OR
// OTHERWISE, ARISING FROM, OUT OF OR IN CONNECTION WITH THE SOFTWARE OR THE USE OR OTHER
// DEALINGS IN THE SOFTWARE.

using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.Linq;
using System.Text;
using System.Windows;
using dnSpy.Contracts.Files.Tabs.TextEditor;
using dnSpy.Contracts.Text;
using dnSpy.Decompiler.Shared;
using ICSharpCode.AvalonEdit.Document;
using ICSharpCode.AvalonEdit.Rendering;

namespace dnSpy.Shared.Decompiler {
	/// <summary>
	/// A text segment that references some object. Used for hyperlinks in the editor.
	/// </summary>
	public sealed class ReferenceSegment : TextSegment {
		public object Reference;
		public bool IsLocal;
		public bool IsLocalTarget;

		public bool Equals(CodeReference codeRef) {
			return codeRef != null &&
				Reference == codeRef.Reference &&
				IsLocal == codeRef.IsLocal &&
				IsLocalTarget == codeRef.IsDefinition;
		}

		public CodeReference ToCodeReference() => new CodeReference(Reference, IsLocal, IsLocalTarget);
	}

	/// <summary>
	/// Stores the positions of the definitions that were written to the text output.
	/// </summary>
	public sealed class DefinitionLookup {
		readonly Dictionary<object, int> definitions = new Dictionary<object, int>();

		public int GetDefinitionPosition(object definition) {
			int val;
			if (definition != null && definitions.TryGetValue(definition, out val))
				return val;
			else
				return -1;
		}

		public void AddDefinition(object definition, int offset) {
			if (definition != null)
				definitions[definition] = offset;
		}
	}

	/// <summary>
	/// Text output implementation for AvalonEdit.
	/// </summary>
	public sealed class AvalonEditTextOutput : ISmartTextOutput {
		public bool CanBeCached {
			get { return canBeCached; }
			private set { canBeCached = value; }
		}
		bool canBeCached = true;

		public CachedTextTokenColors CachedColors => cachedTextTokenColors;
		readonly CachedTextTokenColors cachedTextTokenColors = new CachedTextTokenColors();

		int lastLineStart = 0;
		int lineNumber = 1;
		StringBuilder b = new StringBuilder();

		/// <summary>Current indentation level</summary>
		int indent;
		/// <summary>Whether indentation should be inserted on the next write</summary>
		bool needsIndent;

		public readonly List<VisualLineElementGenerator> ElementGenerators = new List<VisualLineElementGenerator>();

		/// <summary>List of all references that were written to the output</summary>
		readonly TextSegmentCollection<ReferenceSegment> references = new TextSegmentCollection<ReferenceSegment>();

		public readonly DefinitionLookup DefinitionLookup = new DefinitionLookup();

		/// <summary>Embedded UIElements, see <see cref="UIElementGenerator"/>.</summary>
		public readonly List<KeyValuePair<int, Lazy<UIElement>>> UIElements = new List<KeyValuePair<int, Lazy<UIElement>>>();

		public readonly List<MemberMapping> DebuggerMemberMappings = new List<MemberMapping>();

		public AvalonEditTextOutput() {
		}

		/// <summary>
		/// Gets the list of references (hyperlinks).
		/// </summary>
		public TextSegmentCollection<ReferenceSegment> References => references;

		public void AddVisualLineElementGenerator(VisualLineElementGenerator elementGenerator) =>
			ElementGenerators.Add(elementGenerator);

		public int TextLength => b.Length;
		public override string ToString() => b.ToString();
		public TextPosition Location => new TextPosition(lineNumber - 1, b.Length - lastLineStart + 1 + (needsIndent ? indent : 0) - 1);

		public string GetCachedText() {
			if (cachedText != null)
				return cachedText;
			cachedText = b.ToString();
			b = null;
			return cachedText;
		}
		string cachedText;

		public void Indent() => indent++;
		public void Unindent() => indent--;

		void WriteIndent() {
			Debug.Assert(cachedText == null);
			if (needsIndent) {
				needsIndent = false;
				for (int i = 0; i < indent; i++) {
					Append(BoxedOutputColor.Text, "\t");
				}
			}
		}

		public void Write(string text, int index, int count, object data) {
			if (index == 0 && text.Length == count)
				Write(text, data);
			Write(text.Substring(index, count), data);
		}

		public void Write(StringBuilder sb, int index, int count, object data) {
			if (index == 0 && sb.Length == count)
				Write(sb.ToString(), data);
			Write(sb.ToString(index, count), data);
		}

		public void Write(string text, object data) {
			WriteIndent();
			Append(data, text);
		}

		public void WriteLine() {
			Debug.Assert(cachedText == null);
			AppendLine();
			needsIndent = true;
			lastLineStart = b.Length;
			lineNumber++;
		}

		public void WriteDefinition(string text, object definition, object data, bool isLocal) {
			WriteIndent();
			int start = this.TextLength;
			Append(data, text);
			int end = this.TextLength;
			this.DefinitionLookup.AddDefinition(definition, this.TextLength);
			references.Add(new ReferenceSegment { StartOffset = start, EndOffset = end, Reference = definition, IsLocal = isLocal, IsLocalTarget = true });
		}

		public void WriteReference(string text, object reference, object data, bool isLocal) {
			WriteIndent();
			int start = this.TextLength;
			Append(data, text);
			int end = this.TextLength;
			references.Add(new ReferenceSegment { StartOffset = start, EndOffset = end, Reference = reference, IsLocal = isLocal });
		}

		public void AddUIElement(Func<UIElement> element) {
			if (element != null) {
				if (this.UIElements.Count > 0 && this.UIElements.Last().Key == this.TextLength)
					throw new InvalidOperationException("Only one UIElement is allowed for each position in the document");
				this.UIElements.Add(new KeyValuePair<int, Lazy<UIElement>>(this.TextLength, new Lazy<UIElement>(element)));
				DontCacheOutput();
			}
		}

		public void AddDebugSymbols(MemberMapping methodDebugSymbols) => DebuggerMemberMappings.Add(methodDebugSymbols);

		void Append(object data, string s) {
			cachedTextTokenColors.Append(data, s);
			b.Append(s);
			Debug.Assert(b.Length == cachedTextTokenColors.Length);
		}

		public void Write(string text, TextTokenKind tokenKind) =>
			Write(text, tokenKind.Box());
		public void Write(string text, int index, int count, TextTokenKind tokenKind) =>
			Write(text, index, count, tokenKind.Box());
		public void Write(StringBuilder sb, int index, int count, TextTokenKind tokenKind) =>
			Write(sb, index, count, tokenKind.Box());
		public void WriteDefinition(string text, object definition, TextTokenKind tokenKind, bool isLocal = true) =>
			WriteDefinition(text, definition, tokenKind.Box(), isLocal);
		public void WriteReference(string text, object reference, TextTokenKind tokenKind, bool isLocal = false) =>
			WriteReference(text, reference, tokenKind.Box(), isLocal);

		void AppendLine() {
			cachedTextTokenColors.AppendLine();
			b.AppendLine();
			Debug.Assert(b.Length == cachedTextTokenColors.Length);
		}

		public void DontCacheOutput() => CanBeCached = false;
	}
}
