// Copyright (c) 2014 AlphaSierraPapa for the SharpDevelop Team
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
using System.Globalization;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Media;
using System.Windows.Media.TextFormatting;
using ICSharpCode.AvalonEdit.Rendering;

namespace ICSharpCode.AvalonEdit.Utils {
	/// <summary>
	/// Provider of ITextFormatter
	/// </summary>
	public enum TextFormatterProvider {
		/// <summary>
		/// Use internal WPF TextFormatter
		/// </summary>
		BuiltIn,
		/// <summary>
		/// Use custom GlyphRunFormatter
		/// </summary>
		GlyphRunFormatter
	}

	/// <summary>
	/// Interface to TextFormatter
	/// </summary>
	public interface ITextFormatter : IDisposable {
		/// <summary> 
		/// Client to format a text line that fills a paragraph in the document.
		/// </summary> 
		/// <param name="textSource">an object representing text layout clients text source for TextFormatter.</param> 
		/// <param name="firstCharIndex">character index to specify where in the source text the line starts</param>
		/// <param name="paragraphWidth">width of paragraph in which the line fills</param> 
		/// <param name="paragraphProperties">properties that can change from one paragraph to the next, such as text flow direction, text alignment, or indentation.</param>
		/// <param name="previousLineBreak">text formatting state at the point where the previous line in the paragraph
		/// was broken by the text formatting process, as specified by the TextLine.LineBreak property for the previous
		/// line; this parameter can be null, and will always be null for the first line in a paragraph.</param> 
		/// <returns>object representing a line of text that client interacts with. </returns>
		TextLine FormatLine(TextSource textSource, int firstCharIndex, double paragraphWidth,
			TextParagraphProperties paragraphProperties, TextLineBreak previousLineBreak);
	}

	internal class WpfTextFormatter : ITextFormatter {
		TextFormatter formatter;
#if DOTNET4
		public WpfTextFormatter(TextFormattingMode mode) {
			formatter = TextFormatter.Create(mode);
		}
#else
		public WpfTextFormatter(object mode) {
			if (TextFormatterFactory.TextFormattingModeProperty != null) {
				return (TextFormatter)typeof(TextFormatter).InvokeMember(
					"Create",
					BindingFlags.InvokeMethod | BindingFlags.Public | BindingFlags.Static,
					null, null,
					new object[] { mode },
					CultureInfo.InvariantCulture);
			}
			else {
				return TextFormatter.Create();
			}
		}
#endif

		public TextLine FormatLine(TextSource textSource, int firstCharIndex, double paragraphWidth,
			TextParagraphProperties paragraphProperties, TextLineBreak previousLineBreak) {
			return formatter.FormatLine(textSource, firstCharIndex, paragraphWidth, paragraphProperties, previousLineBreak);
		}

		public void Dispose() {
			formatter.Dispose();
		}
	}

	/// <summary>
	/// Creates TextFormatter instances that with the correct TextFormattingMode, if running on .NET 4.0.
	/// </summary>
	public static class TextFormatterFactory {
#if !DOTNET4
		internal readonly static DependencyProperty TextFormattingModeProperty;
		
		static TextFormatterFactory()
		{
			Assembly presentationFramework = typeof(FrameworkElement).Assembly;
			Type textOptionsType = presentationFramework.GetType("System.Windows.Media.TextOptions", false);
			if (textOptionsType != null) {
				TextFormattingModeProperty = textOptionsType.GetField("TextFormattingModeProperty").GetValue(null) as DependencyProperty;
			}
		}
#endif

		public static TextFormatterProvider TextFormatterProvider = TextFormatterProvider.BuiltIn;

		/// <summary>
		/// Creates a <see cref="TextFormatter"/> using the formatting mode used by the specified owner object.
		/// </summary>
		public static ITextFormatter Create(DependencyObject owner) {
			if (owner == null)
				throw new ArgumentNullException("owner");
#if DOTNET4
			switch (TextFormatterProvider) {
				case TextFormatterProvider.BuiltIn:
					return new WpfTextFormatter(TextOptions.GetTextFormattingMode(owner));
				case TextFormatterProvider.GlyphRunFormatter:
					return new GlyphRunFormatter(TextOptions.GetTextFormattingMode(owner));
			}
			return null;
#else
			object formattingMode = null;
			if (TextFormattingModeProperty != null) {
				formattingMode = owner.GetValue(TextFormattingModeProperty);
			}

			switch (TextFormatterProvider) {
				case TextFormatterProvider.BuiltIn:
					return new WpfTextFormatter(formattingMode);
				case TextFormatterProvider.GlyphRunFormatter:
					return new Rendering.GlyphRunFormatter(formattingMode);
			}
#endif
		}

		/// <summary>
		/// Returns whether the specified dependency property affects the text formatter creation.
		/// Controls should re-create their text formatter for such property changes.
		/// </summary>
		public static bool PropertyChangeAffectsTextFormatter(DependencyProperty dp) {
#if DOTNET4
			return dp == TextOptions.TextFormattingModeProperty;
#else
			return dp == TextFormattingModeProperty && TextFormattingModeProperty != null;
#endif
		}

		public static TextLine CreateTextLine(FrameworkElement element, string text, Typeface typeface, double? emSize, Brush foreground) {
			if (element == null)
				throw new ArgumentNullException("element");
			if (text == null)
				throw new ArgumentNullException("text");
			if (typeface == null)
				typeface = element.CreateTypeface();
			if (emSize == null)
				emSize = TextBlock.GetFontSize(element);
			if (foreground == null)
				foreground = TextBlock.GetForeground(element);
#if DOTNET4

			var formatter = Create(element);
			var textRunProps = new GlobalTextRunProperties {
				typeface = typeface,
				foregroundBrush = foreground,
				fontRenderingEmSize = emSize.Value,
				cultureInfo = CultureInfo.CurrentCulture
			};
			var line = formatter.FormatLine(
				new SimpleTextSource(text, textRunProps),
				0, 32000, new VisualLineTextParagraphProperties {
					defaultTextRunProperties = textRunProps
				}, null);
			return line;

#else
			if (TextFormattingModeProperty != null) {
				object formattingMode = element.GetValue(TextFormattingModeProperty);
				return (FormattedText)Activator.CreateInstance(
					typeof(FormattedText),
					text,
					CultureInfo.CurrentCulture,
					FlowDirection.LeftToRight,
					typeface,
					emSize,
					foreground,
					null,
					formattingMode
				);
			} else {
				return new FormattedText(
					text,
					CultureInfo.CurrentCulture,
					FlowDirection.LeftToRight,
					typeface,
					emSize.Value,
					foreground
				);
			}
#endif
		}
	}
}