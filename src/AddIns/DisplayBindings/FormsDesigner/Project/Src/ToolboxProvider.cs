﻿// <file>
//     <copyright see="prj:///doc/copyright.txt"/>
//     <license see="prj:///doc/license.txt"/>
//     <owner name="Mike Krüger" email="mike@icsharpcode.net"/>
//     <version>$Revision$</version>
// </file>

using System;
using System.Collections.Generic;
using System.Drawing.Design;
using System.IO;
using System.Reflection;

using ICSharpCode.Core;
using ICSharpCode.FormsDesigner.Gui;
using ICSharpCode.SharpDevelop;
using ICSharpCode.SharpDevelop.Dom;
using ICSharpCode.SharpDevelop.Gui;
using ICSharpCode.SharpDevelop.Project;
using ICSharpCode.SharpDevelop.Widgets.SideBar;

namespace ICSharpCode.FormsDesigner
{
	public class ToolboxProvider
	{
		static ICSharpCode.FormsDesigner.Services.ToolboxService toolboxService = null;
		public static List<SideTab> SideTabs = new List<SideTab>();
		
		static CustomComponentsSideTab customTab;
		
		static ComponentLibraryLoader componentLibraryLoader = new ComponentLibraryLoader();

		public static ComponentLibraryLoader ComponentLibraryLoader {
			get {
				return componentLibraryLoader;
			}
		}
		public static ICSharpCode.FormsDesigner.Services.ToolboxService ToolboxService {
			get {
				CreateToolboxService();
				return toolboxService;
			}
		}
		
		static void CreateToolboxService()
		{
			if (toolboxService == null) {
				toolboxService = new ICSharpCode.FormsDesigner.Services.ToolboxService();
				ReloadSideTabs(false);
				toolboxService.SelectedItemUsed += new EventHandler(SelectedToolUsedHandler);
				SharpDevelopSideBar.SideBar.SideTabDeleted += SideTabDeleted;
			}
		}
		
		static ToolboxProvider()
		{
			PadDescriptor pad = WorkbenchSingleton.Workbench.GetPad(typeof(SideBarView));
			pad.CreatePad();
			LoadToolbox();
		}
		static string componentLibraryFile = "SharpDevelopControlLibrary.sdcl";
		
		static string GlobalConfigFile {
			get {
				return PropertyService.DataDirectory + Path.DirectorySeparatorChar + 
				       "options" + Path.DirectorySeparatorChar +
				       componentLibraryFile;
			}
		}
		
		static string UserConfigFile {
			get {
				return Path.Combine(PropertyService.ConfigDirectory, componentLibraryFile);
			}
		}
		
		public static void SaveToolbox()
		{
			componentLibraryLoader.SaveToolComponentLibrary(UserConfigFile);
		}
		
		public static void LoadToolbox()
		{
			if (!componentLibraryLoader.LoadToolComponentLibrary(UserConfigFile)) {
				if (!componentLibraryLoader.LoadToolComponentLibrary(GlobalConfigFile)) {
					
					MessageService.ShowWarning("${res:ICSharpCode.SharpDevelop.FormDesigner.ToolboxProvider.CantLoadSidbarComponentLibraryWarning}");
				}
			}
		}
		
		public static void ReloadSideTabs(bool doInsert)
		{
			CreateToolboxService();
			bool reInsertTabs = false;
			foreach(SideTab tab in SideTabs) {
				tab.ItemRemoved -= SideTabItemRemoved;
				tab.ItemsExchanged -= SideTabItemsExchanged;
				if (SharpDevelopSideBar.SideBar.Tabs.Contains(tab)) {
					SharpDevelopSideBar.SideBar.Tabs.Remove(tab);
					reInsertTabs = true;
				}
			}
			reInsertTabs &= doInsert;
			
			SideTabs.Clear();
			foreach (Category category in componentLibraryLoader.Categories) {
				if (category.IsEnabled) {
					try {
						SideTabDesigner newTab = new SideTabDesigner(SharpDevelopSideBar.SideBar, category, toolboxService);
						newTab.ItemRemoved += SideTabItemRemoved;
						newTab.ItemsExchanged += SideTabItemsExchanged;
						SideTabs.Add(newTab);
					} catch (Exception e) {
						ICSharpCode.Core.LoggingService.Warn("Can't add tab : " + e);
					}
				}
			}
			if (customTab != null) {
				customTab.Dispose();
			}
			customTab = new CustomComponentsSideTab(SharpDevelopSideBar.SideBar, ResourceService.GetString("ICSharpCode.SharpDevelop.FormDesigner.ToolboxProvider.CustomComponents"), toolboxService);
			customTab.ItemRemoved += SideTabItemRemoved;
			customTab.ItemsExchanged += SideTabItemsExchanged;
			SideTabs.Add(customTab);
			if (reInsertTabs) {
				foreach(SideTab tab in SideTabs) {
					SharpDevelopSideBar.SideBar.Tabs.Add(tab);
				}
			}
			
			// Clear selected toolbox item after reloading the tabs.
			toolboxService.SetSelectedToolboxItem(null);
		}
		
		static void SelectedToolUsedHandler(object sender, EventArgs e)
		{
			LoggingService.Debug("SelectedToolUsedHandler");
			SideTab tab = SharpDevelopSideBar.SideBar.ActiveTab;
						
			// try to add project reference
			if (sender != null && sender is ICSharpCode.FormsDesigner.Services.ToolboxService) {
				ToolboxItem selectedItem = (sender as IToolboxService).GetSelectedToolboxItem();
				if (tab is CustomComponentsSideTab) {
					if (selectedItem != null && selectedItem.TypeName != null) {
						LoggingService.Debug("Checking for reference to CustomComponent: " + selectedItem.TypeName);
						// Check current project has the custom component first.
						IProjectContent currentProjectContent = ParserService.CurrentProjectContent;
						if (currentProjectContent != null) {
							if (currentProjectContent.GetClass(selectedItem.TypeName, 0) == null) {
								// Check other projects in the solution.
								LoggingService.Debug("Checking other projects in the solution.");
								IProject projectContainingType = FindProjectContainingType(selectedItem.TypeName);
								if (projectContainingType != null) {
									AddProjectReferenceToProject(ProjectService.CurrentProject, projectContainingType);
								}
							}
						}
					}
				} else {
					if (selectedItem != null && selectedItem.AssemblyName != null) {
						IProject currentProject = ProjectService.CurrentProject;					
						if (currentProject != null) {
							if (!ProjectContainsReference(currentProject, selectedItem.AssemblyName)) {
								AddReferenceToProject(currentProject, selectedItem.AssemblyName);
							}
						} 
					} 
				}
			}
			
			if (tab.Items.Count > 0) {
				tab.ChoosedItem = tab.Items[0];
			}
			SharpDevelopSideBar.SideBar.Refresh();
		}
			
		static bool ProjectContainsReference(IProject project, AssemblyName referenceName)
		{
			LoggingService.Debug("Checking project has reference: " + referenceName.FullName);
			bool isAlreadyInRefFolder = false;
		
			foreach (ProjectItem projectItem in project.Items) {
				ReferenceProjectItem referenceItem = projectItem as ReferenceProjectItem;
				if (referenceItem != null) {
					if (referenceItem.ItemType == ItemType.Reference) {
						LoggingService.Debug("Checking project reference: " + referenceItem.Include);
						if (referenceItem.HintPath.Length > 0 && File.Exists(referenceItem.FileName)) {
							LoggingService.Debug("Checking assembly reference");
							AssemblyName assemblyName = AssemblyName.GetAssemblyName(referenceItem.FileName);
							if (assemblyName != null && assemblyName.FullName == referenceName.FullName) {
								isAlreadyInRefFolder = true;
								break;
							}
						} else { // GAC reference.
							LoggingService.Debug("Checking GAC reference");
							if (referenceItem.Include == referenceName.FullName || referenceItem.Include == referenceName.Name) {
								LoggingService.Debug("Found existing GAC reference");
								isAlreadyInRefFolder = true;
								break;
							}
						}
					}
				}
			}
			return isAlreadyInRefFolder;
		}
		
		static void AddReferenceToProject(IProject project, AssemblyName referenceName)
		{
			LoggingService.Warn("Adding reference to project: " + referenceName.FullName);
			ReferenceProjectItem reference = new ReferenceProjectItem(project, "Reference");
			ToolComponent toolComponent = ToolboxProvider.ComponentLibraryLoader.GetToolComponent(referenceName.FullName);
			if (toolComponent == null || toolComponent.HintPath == null) {
				reference.Include = referenceName.FullName;
				LoggingService.Debug("Added GAC reference to project: " + reference.Include);
			} else {
				reference.Include = referenceName.FullName;
				reference.HintPath = FileUtility.GetRelativePath(project.Directory, toolComponent.FileName);
				reference.SpecificVersion = false;
				LoggingService.Debug("Added assembly reference to project: " + reference.Include);
			}
			ProjectService.AddProjectItem(project, reference);
			project.Save();
		}
		
		/// <summary>
		/// Looks for the specified type in all the projects in the open solution
		/// excluding the current project.
		/// </summary>
		static IProject FindProjectContainingType(string type)
		{
			IProject currentProject = ProjectService.CurrentProject;
			foreach (IProject project in ProjectService.OpenSolution.Projects) {
				if (project != currentProject) {
					IProjectContent projectContent = ParserService.GetProjectContent(project);
					if (projectContent != null) {
						if (projectContent.GetClass(type, 0) != null) {
							LoggingService.Debug("Found project containing type: " + project.FileName);
							return project;
						}
					}
				}
			}
			return null;
		}

		static void AddProjectReferenceToProject(IProject project, IProject referenceTo)
		{
			LoggingService.Warn("Adding project reference to project.");
			ProjectReferenceProjectItem reference = new ProjectReferenceProjectItem(project, referenceTo);
			ProjectService.AddProjectItem(project, reference);
			project.Save();
		}
		
		static void SideTabDeleted(object source, SideTabEventArgs e)
		{
			if (SideTabs.Contains(e.SideTab)) {
				SideTabs.Remove(e.SideTab);
				componentLibraryLoader.RemoveCategory(e.SideTab.Name);
				SaveToolbox();
			}
		}
		
		static void SideTabItemRemoved(object source, SideTabItemEventArgs e)
		{
			SideTabDesigner tab = source as SideTabDesigner;
			ToolboxItem toolboxItem = e.Item.Tag as ToolboxItem;
			if (tab != null && toolboxItem != null) {
				componentLibraryLoader.DisableToolComponent(tab.Name, toolboxItem.TypeName);
				SaveToolbox();
			}
		}
		
		static void SideTabItemsExchanged(object source, SideTabItemExchangeEventArgs e)
		{
			SideTabDesigner tab = source as SideTabDesigner;
			ToolboxItem toolboxItem1 = e.Item1.Tag as ToolboxItem;
			ToolboxItem toolboxItem2 = e.Item2.Tag as ToolboxItem;
			if (tab != null && toolboxItem1 != null && toolboxItem2 != null) {
				componentLibraryLoader.ExchangeToolComponents(tab.Name, toolboxItem1.TypeName, toolboxItem2.TypeName);
				SaveToolbox();
			}
		}
	}
}
