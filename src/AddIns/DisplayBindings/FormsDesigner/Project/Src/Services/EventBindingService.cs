﻿// <file>
//     <copyright see="prj:///doc/copyright.txt"/>
//     <license see="prj:///doc/license.txt"/>
//     <owner name="Mike Krüger" email="mike@icsharpcode.net"/>
//     <version>$Revision$</version>
// </file>

using System;
using System.Collections;
using System.ComponentModel;
using ICSharpCode.FormsDesigner.Gui.OptionPanels;
using ICSharpCode.SharpDevelop.Gui;

namespace ICSharpCode.FormsDesigner.Services
{
	public class EventBindingService : System.ComponentModel.Design.EventBindingService
	{
		readonly FormsDesignerViewContent formDesigner;
		
		public EventBindingService(FormsDesignerViewContent formDesigner, IServiceProvider provider) : base(provider)
		{
			if (formDesigner == null)
				throw new ArgumentNullException("formDesigner");
			this.formDesigner = formDesigner;
		}

		protected override string CreateUniqueMethodName(IComponent component, EventDescriptor e)
		{
			string componentName = GetComponentName(component);
			return GetEventHandlerName(componentName, e.DisplayName);
		}
		
		string GetComponentName(IComponent component)
		{
			string siteName = component.Site.Name;
			return Char.ToUpper(siteName[0]) + siteName.Substring(1);
		}
		
		string GetEventHandlerName(string componentName, string eventName)
		{
			string eventHandlerNameFormat = GetEventHandlerNameFormat();
			return String.Format(eventHandlerNameFormat, componentName, eventName);
		}
		
		string GetEventHandlerNameFormat()
		{
			if (GeneralOptionsPanel.GenerateVisualStudioStyleEventHandlers) {
				return "{0}_{1}";
			}
			return "{0}{1}";
		}

		// sohuld look around in form class for compatiable methodes
		protected override ICollection GetCompatibleMethods(EventDescriptor e)
		{
			return this.formDesigner.GetCompatibleMethods(e);
		}
		
		protected override bool ShowCode()
		{
			if (formDesigner != null) {
				formDesigner.ShowSourceCode();
				return true;
			}
			return false;
		}

		protected override bool ShowCode(int lineNumber)
		{
			if (formDesigner != null) {
				formDesigner.ShowSourceCode(lineNumber);
				return true;
			}
			return false;
		}

		protected override bool ShowCode(IComponent component, EventDescriptor edesc, string methodName)
		{
			if (formDesigner != null) {
				formDesigner.ShowSourceCode(component, edesc, methodName);
				return true;
			}
			return false;
		}

	}
}
