﻿using Codeer.Friendly;
using System;
using System.Reflection;

namespace VSHTC.Friendly.PinInterface.Inside
{
    class FriendlyProxyInstance<TInterface> : FriendlyProxy<TInterface>
        where TInterface : IAppVarOwner
    {
        private readonly AppVar _appVar;

        public FriendlyProxyInstance(AppVar appVar)
            : base(appVar.App) 
        {
            _appVar = appVar;
        }

        public AppVar AppVar {
            get { return _appVar; }
        }

        protected override AppVar Invoke(MethodInfo method, string name, object[] args)
        {

            if ((method.DeclaringType == typeof(IAppVarOwner) && name == "AppVar"))
            {
                return _appVar;
            }

            return _appVar[name](args);
        }
    }
}
