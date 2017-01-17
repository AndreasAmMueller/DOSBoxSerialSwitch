using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Windows.Controls;

namespace DOSBoxSerialSwitch
{
	public static class Extensions
	{
		public static void AddRange(this ItemCollection items, IEnumerable<object> newItems)
		{
			foreach (var item in newItems)
			{
				items.Add(item);
			}
		}
	}
}
