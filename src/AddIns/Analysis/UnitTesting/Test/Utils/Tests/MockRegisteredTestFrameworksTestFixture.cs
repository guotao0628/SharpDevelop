﻿// <file>
//     <copyright see="prj:///doc/copyright.txt"/>
//     <license see="prj:///doc/license.txt"/>
//     <owner name="Matthew Ward" email="mrward@users.sourceforge.net"/>
//     <version>$Revision$</version>
// </file>

using System;
using ICSharpCode.UnitTesting;
using NUnit.Framework;
using UnitTesting.Tests.Utils;

namespace UnitTesting.Tests.Utils.Tests
{
	[TestFixture]
	public class MockRegisteredTestFrameworksTestFixture
	{
		MockRegisteredTestFrameworks testFrameworks;
		MockTestFramework testFramework;
		MockCSharpProject project;
		
		[SetUp]
		public void Init()
		{
			testFrameworks = new MockRegisteredTestFrameworks();
			
			testFramework = new MockTestFramework();
			project = new MockCSharpProject();
			testFrameworks.AddTestFrameworkForProject(project, testFramework);
		}
		
		[Test]
		public void CreateTestRunnerForKnownProjectCreatesTestRunnerInKnownTestFramework()
		{
			ITestRunner testRunner = testFrameworks.CreateTestRunner(project);
			ITestRunner expectedTestRunner = testFramework.TestRunnersCreated[0];
			Assert.AreEqual(expectedTestRunner, testRunner);
		}
		
		[Test]
		public void CreateTestRunnerReturnsNullForUnknownProject()
		{
			MockCSharpProject unknownProject = new MockCSharpProject();
			Assert.IsNull(testFrameworks.CreateTestRunner(unknownProject));
		}
		
		[Test]
		public void GetTestFrameworkForProjectReturnsTestFrameworkForKnownProject()
		{
			Assert.AreEqual(testFramework, testFrameworks.GetTestFrameworkForProject(project));
		}
		
		[Test]
		public void GetTestFrameworkForProjectReturnsTestFrameworkForUnknownProject()
		{
			MockCSharpProject unknownProject = new MockCSharpProject();
			Assert.IsNull(testFrameworks.GetTestFrameworkForProject(unknownProject));
		}
		
		[Test]
		public void CreateTestDebuggerForKnownProjectCreatesTestDebuggerInKnownTestFramework()
		{
			ITestRunner testRunner = testFrameworks.CreateTestDebugger(project);
			ITestRunner expectedTestRunner = testFramework.TestDebuggersCreated[0];
			Assert.AreEqual(expectedTestRunner, testRunner);
		}

		[Test]
		public void CreateTestDebuggerReturnsNullForUnknownProject()
		{
			MockCSharpProject unknownProject = new MockCSharpProject();
			Assert.IsNull(testFrameworks.CreateTestDebugger(unknownProject));
		}
	}
}
