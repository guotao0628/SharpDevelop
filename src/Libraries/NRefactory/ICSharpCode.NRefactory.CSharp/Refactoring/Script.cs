﻿// 
// Script.cs
//
// Author:
//       Mike Krüger <mkrueger@novell.com>
// 
// Copyright (c) 2011 Mike Krüger <mkrueger@novell.com>
// 
// Permission is hereby granted, free of charge, to any person obtaining a copy
// of this software and associated documentation files (the "Software"), to deal
// in the Software without restriction, including without limitation the rights
// to use, copy, modify, merge, publish, distribute, sublicense, and/or sell
// copies of the Software, and to permit persons to whom the Software is
// furnished to do so, subject to the following conditions:
// 
// The above copyright notice and this permission notice shall be included in
// all copies or substantial portions of the Software.
// 
// THE SOFTWARE IS PROVIDED "AS IS", WITHOUT WARRANTY OF ANY KIND, EXPRESS OR
// IMPLIED, INCLUDING BUT NOT LIMITED TO THE WARRANTIES OF MERCHANTABILITY,
// FITNESS FOR A PARTICULAR PURPOSE AND NONINFRINGEMENT. IN NO EVENT SHALL THE
// AUTHORS OR COPYRIGHT HOLDERS BE LIABLE FOR ANY CLAIM, DAMAGES OR OTHER
// LIABILITY, WHETHER IN AN ACTION OF CONTRACT, TORT OR OTHERWISE, ARISING FROM,
// OUT OF OR IN CONNECTION WITH THE SOFTWARE OR THE USE OR OTHER DEALINGS IN
// THE SOFTWARE.
using System;
using System.Collections.Generic;
using System.IO;
using System.Text;
using ICSharpCode.NRefactory.Editor;
using ICSharpCode.NRefactory.TypeSystem;

namespace ICSharpCode.NRefactory.CSharp.Refactoring
{
	/// <summary>
	/// Class for creating change scripts.
	/// 'Original document' = document without the change script applied.
	/// 'Current document' = document with the change script (as far as it is already created) applies.
	/// </summary>
	public abstract class Script : IDisposable
	{
		internal struct Segment : ISegment
		{
			readonly int offset;
			readonly int length;
			
			public int Offset {
				get { return offset; }
			}
			
			public int Length {
				get { return length; }
			}
			
			public int EndOffset {
				get { return Offset + Length; }
			}
			
			public Segment (int offset, int length)
			{
				this.offset = offset;
				this.length = length;
			}
			
			public override string ToString ()
			{
				return string.Format ("[Script.Segment: Offset={0}, Length={1}, EndOffset={2}]", Offset, Length, EndOffset);
			}
		}
		
		readonly string eolMarker;
		readonly CSharpFormattingOptions formattingOptions;
		Dictionary<AstNode, ISegment> segmentsForInsertedNodes = new Dictionary<AstNode, ISegment>();
		
		protected Script(string eolMarker, CSharpFormattingOptions formattingOptions)
		{
			if (eolMarker == null)
				throw new ArgumentNullException("eolMarker");
			if (formattingOptions == null)
				throw new ArgumentNullException("formattingOptions");
			this.eolMarker = eolMarker;
			this.formattingOptions = formattingOptions;
		}
		
		/// <summary>
		/// Given an offset in the original document (at the start of script execution),
		/// returns the offset in the current document.
		/// </summary>
		public abstract int GetCurrentOffset(int originalDocumentOffset);
		
		/// <summary>
		/// Given an offset in the original document (at the start of script execution),
		/// returns the offset in the current document.
		/// </summary>
		public abstract int GetCurrentOffset(TextLocation originalDocumentLocation);
		
		/// <summary>
		/// Creates a tracked segment for the specified (offset,length)-segment.
		/// Offset is interpreted to be an offset in the current document.
		/// </summary>
		/// <returns>
		/// A segment that initially has the specified values, and updates
		/// on every <see cref="Replace(int,int,string)"/> call.
		/// </returns>
		protected abstract ISegment CreateTrackedSegment(int offset, int length);
		
		protected ISegment GetSegment(AstNode node)
		{
			ISegment segment;
			if (segmentsForInsertedNodes.TryGetValue(node, out segment))
				return segment;
			if (node.StartLocation.IsEmpty || node.EndLocation.IsEmpty) {
				throw new InvalidOperationException("Trying to get the position of a node that is not part of the original document and was not inserted");
			}
			int startOffset = GetCurrentOffset(node.StartLocation);
			int endOffset = GetCurrentOffset(node.EndLocation);
			return new Segment(startOffset, endOffset - startOffset);
		}
		
		/// <summary>
		/// Replaces text.
		/// </summary>
		/// <param name="offset">The starting offset of the text to be replaced.</param>
		/// <param name="length">The length of the text to be replaced.</param>
		/// <param name="newText">The new text.</param>
		public abstract void Replace (int offset, int length, string newText);
		
		public void InsertText(int offset, string newText)
		{
			Replace(offset, 0, newText);
		}
		
		public CSharpFormattingOptions FormattingOptions {
			get { return formattingOptions; }
		}
		
		public void Remove (AstNode node)
		{
			var segment = GetSegment(node);
			Replace(segment.Offset, segment.Length, string.Empty);
		}
		
		public void InsertBefore (AstNode node, AstNode insertNode)
		{
			var startOffset = GetCurrentOffset (new TextLocation(node.StartLocation.Line, 1));
			var output = OutputNode (GetIndentLevelAt (startOffset), insertNode);
			string text = output.Text;
			if (!(insertNode is Expression || insertNode is AstType))
				text += eolMarker;
			InsertText(startOffset, text);
			output.RegisterTrackedSegments(this, startOffset);
		}

		public void AddTo (BlockStatement bodyStatement, AstNode insertNode)
		{
			var startOffset = GetCurrentOffset (bodyStatement.LBraceToken.EndLocation);
			var output = OutputNode (1 + GetIndentLevelAt (startOffset), insertNode, true);
			InsertText (startOffset, output.Text);
			output.RegisterTrackedSegments (this, startOffset);
		}
		
		public virtual void Link (params AstNode[] nodes)
		{
			// Default implementation: do nothing
			// Derived classes are supposed to enter the text editor's linked state.
		}
		
		public void Replace (AstNode node, AstNode replaceWith)
		{
			var segment = GetSegment (node);
			int startOffset = segment.Offset;
			int level = 0;
			if (!(replaceWith is Expression) && !(replaceWith is AstType))
				level = GetIndentLevelAt (startOffset);
			NodeOutput output = OutputNode (level, replaceWith);
			output.TrimStart ();
			Replace (startOffset, segment.Length, output.Text);
			output.RegisterTrackedSegments(this, startOffset);
		}
		
		public void FormatText (AstNode node)
		{
			var segment = GetSegment(node);
			FormatText(segment.Offset, segment.Length);
		}
		
		public abstract void FormatText (int offset, int length);
		
		public void Select (AstNode node)
		{
			var segment = GetSegment(node);
			Select(segment.Offset, segment.Length);
		}
		
		public virtual void Select (int offset, int length)
		{
			// default implementation: do nothing
			// Derived classes are supposed to set the text editor's selection
		}
		
		public enum InsertPosition {
			Start,
			Before,
			After,
			End
		}
		
		public abstract void InsertWithCursor (string operation, AstNode node, InsertPosition defaultPosition);

		protected virtual int GetIndentLevelAt (int offset)
		{
			return 0;
		}
		
		sealed class SegmentTrackingOutputFormatter : TextWriterOutputFormatter
		{
			internal List<KeyValuePair<AstNode, Segment>> NewSegments = new List<KeyValuePair<AstNode, Segment>>();
			Stack<int> startOffsets = new Stack<int>();
			readonly StringWriter stringWriter;
			
			public SegmentTrackingOutputFormatter(StringWriter stringWriter)
				: base(stringWriter)
			{
				this.stringWriter = stringWriter;
			}
			
			public override void StartNode(AstNode node)
			{
				base.StartNode(node);
				startOffsets.Push(stringWriter.GetStringBuilder ().Length);
			}
			
			public override void EndNode(AstNode node)
			{
				int startOffset = startOffsets.Pop();
				int endOffset = stringWriter.GetStringBuilder ().Length;
				NewSegments.Add(new KeyValuePair<AstNode, Segment>(node, new Segment(startOffset, endOffset - startOffset)));
				base.EndNode(node);
			}
		}
		
		protected NodeOutput OutputNode(int indentLevel, AstNode node, bool startWithNewLine = false)
		{
			var stringWriter = new StringWriter ();
			var formatter = new SegmentTrackingOutputFormatter (stringWriter);
			formatter.Indentation = indentLevel;
			stringWriter.NewLine = eolMarker;
			if (startWithNewLine)
				formatter.NewLine ();
			var visitor = new CSharpOutputVisitor (formatter, formattingOptions);
			node.AcceptVisitor (visitor, null);
			string text = stringWriter.ToString().TrimEnd();
			
			if (node is FieldDeclaration)
				text += eolMarker;
			return new NodeOutput(text, formatter.NewSegments);
		}
		
		protected class NodeOutput
		{
			string text;
			List<KeyValuePair<AstNode, Segment>> newSegments;
			int trimmedLength;
			
			internal NodeOutput(string text, List<KeyValuePair<AstNode, Segment>> newSegments)
			{
				this.text = text;
				this.newSegments = newSegments;
			}
			
			public string Text {
				get { return text; }
			}
			
			public void TrimStart()
			{
				for (int i = 0; i < text.Length; i++) {
					char ch = text [i];
					if (ch != ' ' && ch != '\t') {
						if (i > 0) {
							text = text.Substring (i);
							trimmedLength = i;
						}
						break;
					}
				}
			}
			
			public void RegisterTrackedSegments(Script script, int insertionOffset)
			{
				foreach (var pair in newSegments) {
					int offset = insertionOffset + pair.Value.Offset - trimmedLength;
					ISegment trackedSegment = script.CreateTrackedSegment(offset, pair.Value.Length);
					script.segmentsForInsertedNodes.Add(pair.Key, trackedSegment);
				}
			}
		}
		
		/// <summary>
		/// Performs a rename refactoring.
		/// </summary>
		public abstract void Rename(IEntity entity, string name);
		
		public abstract void Dispose();
	}
}
