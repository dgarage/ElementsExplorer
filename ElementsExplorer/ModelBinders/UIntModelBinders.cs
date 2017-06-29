﻿using Microsoft.AspNetCore.Mvc.Internal;
using Microsoft.AspNetCore.Mvc.ModelBinding;
using NBitcoin;
using System;
using System.Reflection;
using System.Threading.Tasks;

namespace ElementsExplorer.ModelBinders
{
    public class UInt256ModelBinding : IModelBinder
    {
		#region IModelBinder Members

		public Task BindModelAsync(ModelBindingContext bindingContext)
		{
            if(!typeof(uint256).GetTypeInfo().IsAssignableFrom(bindingContext.ModelType))
            {
				return TaskCache.CompletedTask;
			}

            ValueProviderResult val = bindingContext.ValueProvider.GetValue(
                bindingContext.ModelName);
            if(val == null)
            {
				return TaskCache.CompletedTask;
			}

            string key = val.FirstValue as string;
			if(key == null)
            {
				bindingContext.Result = ModelBindingResult.Success(null);
				return TaskCache.CompletedTask;
			}
            var value = uint256.Parse(key);
			bindingContext.Result = ModelBindingResult.Success(value);
			return TaskCache.CompletedTask;
		}

        #endregion
    }

    public class UInt160ModelBinding : IModelBinder
    {
		#region IModelBinder Members

		public Task BindModelAsync(ModelBindingContext bindingContext)
		{
            if(!typeof(uint160).GetTypeInfo().IsAssignableFrom(bindingContext.ModelType))
            {
				return TaskCache.CompletedTask;
			}

            ValueProviderResult val = bindingContext.ValueProvider.GetValue(
                bindingContext.ModelName);
            if(val == null)
            {
				return TaskCache.CompletedTask;
			}

            string key = val.FirstValue as string;
			if(key == null)
            {
                bindingContext.Model = null;
				return TaskCache.CompletedTask;				
			}
            var value = uint160.Parse(key);
            if(value.ToString().StartsWith(uint160.Zero.ToString()))
                throw new FormatException("Invalid hash format");
			bindingContext.Result = ModelBindingResult.Success(value);
			return TaskCache.CompletedTask;
		}

        #endregion
    }
}
