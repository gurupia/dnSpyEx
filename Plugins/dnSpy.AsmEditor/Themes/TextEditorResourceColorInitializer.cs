﻿/*
    Copyright (C) 2014-2016 de4dot@gmail.com

    This file is part of dnSpy

    dnSpy is free software: you can redistribute it and/or modify
    it under the terms of the GNU General Public License as published by
    the Free Software Foundation, either version 3 of the License, or
    (at your option) any later version.

    dnSpy is distributed in the hope that it will be useful,
    but WITHOUT ANY WARRANTY; without even the implied warranty of
    MERCHANTABILITY or FITNESS FOR A PARTICULAR PURPOSE.  See the
    GNU General Public License for more details.

    You should have received a copy of the GNU General Public License
    along with dnSpy.  If not, see <http://www.gnu.org/licenses/>.
*/

using System.ComponentModel.Composition;
using System.Windows;
using System.Windows.Media;
using dnSpy.Contracts.Plugin;
using dnSpy.Contracts.Text;
using dnSpy.Contracts.Themes;

namespace dnSpy.AsmEditor.Themes {
	[ExportAutoLoaded(LoadType = AutoLoadedLoadType.BeforePlugins)]
	sealed class TextEditorResourceColorInitializer : IAutoLoaded {
		readonly IThemeManager themeManager;

		[ImportingConstructor]
		TextEditorResourceColorInitializer(IThemeManager themeManager) {
			this.themeManager = themeManager;
			this.themeManager.ThemeChanged += ThemeManager_ThemeChanged;
			InitializeResources();
		}

		void ThemeManager_ThemeChanged(object sender, ThemeChangedEventArgs e) => InitializeResources();

		void InitializeResources() {
			var theme = themeManager.Theme;

			foreach (var f in typeof(OutputColor).GetFields()) {
				if (!f.IsLiteral)
					continue;
				var val = (OutputColor)f.GetValue(null);
				if (val != OutputColor.Last)
					UpdateTextEditorResource(val, f.Name);
			}
		}

		void UpdateTextEditorResource(OutputColor colorType, string name) {
			var theme = themeManager.Theme;

			var color = theme.GetTextColor(colorType.ToColorType());
			Application.Current.Resources[GetTextInheritedForegroundResourceKey(name)] = GetBrush(color.Foreground);
			Application.Current.Resources[GetTextInheritedBackgroundResourceKey(name)] = GetBrush(color.Background);
			Application.Current.Resources[GetTextInheritedFontStyleResourceKey(name)] = color.FontStyle ?? FontStyles.Normal;
			Application.Current.Resources[GetTextInheritedFontWeightResourceKey(name)] = color.FontWeight ?? FontWeights.Normal;

			color = theme.GetColor(colorType.ToColorType());
			Application.Current.Resources[GetInheritedForegroundResourceKey(name)] = GetBrush(color.Foreground);
			Application.Current.Resources[GetInheritedBackgroundResourceKey(name)] = GetBrush(color.Background);
			Application.Current.Resources[GetInheritedFontStyleResourceKey(name)] = color.FontStyle ?? FontStyles.Normal;
			Application.Current.Resources[GetInheritedFontWeightResourceKey(name)] = color.FontWeight ?? FontWeights.Normal;
		}

		static Brush GetBrush(Brush b) => b ?? Brushes.Transparent;
		static string GetTextInheritedForegroundResourceKey(string name) => string.Format("TETextInherited{0}Foreground", name);
		static string GetTextInheritedBackgroundResourceKey(string name) => string.Format("TETextInherited{0}Background", name);
		static string GetTextInheritedFontStyleResourceKey(string name) => string.Format("TETextInherited{0}FontStyle", name);
		static string GetTextInheritedFontWeightResourceKey(string name) => string.Format("TETextInherited{0}FontWeight", name);
		static string GetInheritedForegroundResourceKey(string name) => string.Format("TEInherited{0}Foreground", name);
		static string GetInheritedBackgroundResourceKey(string name) => string.Format("TEInherited{0}Background", name);
		static string GetInheritedFontStyleResourceKey(string name) => string.Format("TEInherited{0}FontStyle", name);
		static string GetInheritedFontWeightResourceKey(string name) => string.Format("TEInherited{0}FontWeight", name);
	}
}
