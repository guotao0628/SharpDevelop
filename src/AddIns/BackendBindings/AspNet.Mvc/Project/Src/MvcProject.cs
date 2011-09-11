﻿// Copyright (c) AlphaSierraPapa for the SharpDevelop Team (for details please see \doc\copyright.txt)
// This code is distributed under the GNU LGPL (for details please see \doc\license.txt)

using System;
using System.Collections.Generic;
using ICSharpCode.SharpDevelop.Project;

namespace ICSharpCode.AspNet.Mvc
{
	public class MvcProject : IMvcProject
	{
		IMvcModelClassLocator modelClassLocator;
		
		public MvcProject(IProject project)
			: this(project, new MvcModelClassLocator())
		{
		}
		
		public MvcProject(IProject project, IMvcModelClassLocator modelClassLocator)
		{
			this.Project = project;
			this.modelClassLocator = modelClassLocator;
		}
		
		public IProject Project { get; private set; }
		
		public string RootNamespace {
			get { return Project.RootNamespace; }
		}
		
		public void Save()
		{
			Project.Save();
		}
		
		public MvcTextTemplateLanguage GetTemplateLanguage()
		{
			string language = Project.Language;
			return MvcTextTemplateLanguageConverter.Convert(language);
		}
		
		public IEnumerable<IMvcClass> GetModelClasses()
		{
			return modelClassLocator.GetModelClasses(this);
		}
		
		public IEnumerable<MvcMasterPageFileName> GetAspxMasterPageFileNames()
		{
			foreach (ProjectItem projectItem in Project.Items) {
				MvcMasterPageFileName fileName = MvcMasterPageFileName.CreateMvcMasterPageFileName(projectItem);
				if (fileName != null) {
					yield return fileName;
				}
			}
		}
	}
}
