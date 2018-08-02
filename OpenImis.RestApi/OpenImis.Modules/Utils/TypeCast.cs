﻿using Newtonsoft.Json;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading.Tasks;

namespace OpenImis.Modules.Utils
{
    public static class TypeCast
    {
		public static T Cast<T>(Object parentInstance)
		{
			T result = default(T);
			//try
			//{
			var serializedParent = JsonConvert.SerializeObject(parentInstance);
			result = JsonConvert.DeserializeObject<T>(serializedParent);
			//}
			return result;
		}

	}
}
