﻿using System;
using Codeer.Friendly;
using System.Runtime.Remoting.Proxies;

namespace VSHTC.Friendly.PinInterface.Inside
{
    internal static class FriendlyProxyFactory
    {
        internal static object WrapFriendlyProxyInstance(Type type, AppVar ret)
        {
            return WrapFriendlyProxy(typeof(FriendlyProxyInstance<>), type, new object[] { ret });
        }

        internal static T WrapFriendlyProxy<T>(Type proxyType, params object[] args)
        {
            return (T)WrapFriendlyProxy(proxyType, typeof(T), args);
        }

        internal static object WrapFriendlyProxy(Type proxyType, Type interfaceType, object[] args)
        {
            var friendlyProxyType = proxyType.MakeGenericType(interfaceType);
            RealProxy friendlyProxy = Activator.CreateInstance(friendlyProxyType, args) as RealProxy;
            return friendlyProxy.GetTransparentProxy();
        }
    }
}
