﻿using System;
using System.Collections.Generic;
using System.Runtime.CompilerServices;
using System.Text;

namespace ElementsExplorer.Tests
{
	public class RepositoryTester : IDisposable
	{
		public static RepositoryTester Create(bool caching, [CallerMemberName]string name = null)
		{
			return new RepositoryTester(name, caching);
		}

		string _Name;
		RepositoryTester(string name, bool caching)
		{
			_Name = name;
			ServerTester.DeleteRecursivelyWithMagicDust(name);
			_Repository = new Repository(name, caching);
		}

		public void Dispose()
		{
			_Repository.Dispose();
			ServerTester.DeleteRecursivelyWithMagicDust(_Name);
		}

		public void ReloadRepository(bool caching)
		{
			_Repository.Dispose();
			_Repository = new Repository(_Name, caching);
		}

		private Repository _Repository;
		public Repository Repository
		{
			get
			{
				return _Repository;
			}
		}
	}
}
