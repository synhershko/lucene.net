using System;

namespace Lucene.Net.Util
{

	/*
	 * Licensed to the Apache Software Foundation (ASF) under one or more
	 * contributor license agreements.  See the NOTICE file distributed with
	 * this work for additional information regarding copyright ownership.
	 * The ASF licenses this file to You under the Apache License, Version 2.0
	 * (the "License"); you may not use this file except in compliance with
	 * the License.  You may obtain a copy of the License at
	 *
	 *     http://www.apache.org/licenses/LICENSE-2.0
	 *
	 * Unless required by applicable law or agreed to in writing, software
	 * distributed under the License is distributed on an "AS IS" BASIS,
	 * WITHOUT WARRANTIES OR CONDITIONS OF ANY KIND, either express or implied.
	 * See the License for the specific language governing permissions and
	 * limitations under the License.
	 */

	/*using TestRule = org.junit.rules.TestRule;
	using Description = org.junit.runner.Description;
	using Statement = org.junit.runners.model.Statement;*/

	/// <summary>
	/// Stores the suite name so you can retrieve it
	/// from <seealso cref="#getTestClass()"/>
	/// </summary>
	public class TestRuleStoreClassName : TestRule
	{
	  private volatile Description Description;

	  public override Statement Apply(Statement s, Description d)
	  {
		if (!d.Suite)
		{
		  throw new System.ArgumentException("this is a @ClassRule (applies to suites only).");
		}

		return new StatementAnonymousInnerClassHelper(this, s, d);
	  }

	  private class StatementAnonymousInnerClassHelper : Statement
	  {
		  private readonly TestRuleStoreClassName OuterInstance;

		  private Statement s;
		  private Description d;

		  public StatementAnonymousInnerClassHelper(TestRuleStoreClassName outerInstance, Statement s, Description d)
		  {
			  this.OuterInstance = outerInstance;
			  this.s = s;
			  this.d = d;
		  }

		  public override void Evaluate()
		  {
			try
			{
			  OuterInstance.Description = d;
			  s.evaluate();
			}
			finally
			{
			  OuterInstance.Description = null;
			}
		  }
	  }

	  /// <summary>
	  /// Returns the test class currently executing in this rule.
	  /// </summary>
	  public virtual Type TestClass
	  {
		  get
		  {
			Description localDescription = Description;
			if (localDescription == null)
			{
			  throw new Exception("The rule is not currently executing.");
			}
			return localDescription.TestClass;
		  }
	  }
	}

}