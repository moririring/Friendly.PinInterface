using System;
using System.Linq;
using System.Reflection;
using System.Runtime.Remoting.Messaging;
using System.Runtime.Remoting.Proxies;
using System.Runtime.CompilerServices;
using Codeer.Friendly;
using Codeer.Friendly.Dynamic;
using System.Text;
using System.Collections.Generic;

namespace VSHTC.Friendly.PinInterface.Inside
{
    abstract class FriendlyProxy<TInterface> : RealProxy
    {
        protected AppFriend App { get; private set; }

        Async _asyncNext;
        OperationTypeInfo _operationTypeInfoNext;

        public FriendlyProxy(AppFriend app)
            : base(typeof(TInterface)) 
        {
            App = app;
        }

        public override IMessage Invoke(IMessage msg)
        {
            var mm = msg as IMethodMessage;
            var method = (MethodInfo)mm.MethodBase;

            //GetType�����͑���������
            //�v��ʂƂ���ŌĂяo����A�V���A���C�Y�ł����A�N���b�V�����Ă��܂��B
            if ((method.DeclaringType == typeof(object) && method.Name == "GetType"))
            {
                return new ReturnMessage(typeof(TInterface), null, 0, mm.LogicalCallContext, (IMethodCallMessage)msg);
            }

            //IModifyInvoke�̑Ή�
            if ((method.DeclaringType == typeof(IModifyInvoke) && method.Name == "AsyncNext"))
            {
                if (_asyncNext != null)
                {
                    throw new NotSupportedException("���Ɏ���Ăяo����Async�͐ݒ肳��Ă��܂��B");
                }
                _asyncNext = new Async();
                return new ReturnMessage(_asyncNext, null, 0, mm.LogicalCallContext, (IMethodCallMessage)msg);
            }
            if ((method.DeclaringType == typeof(IModifyInvoke) && method.Name == "OperationTypeInfoNext"))
            {
                if (_operationTypeInfoNext != null)
                {
                    throw new NotSupportedException("���Ɏ���Ăяo����OperationTypeInfo�͐ݒ肳��Ă��܂��B");
                }
                _operationTypeInfoNext = (OperationTypeInfo)mm.Args[0];
                return new ReturnMessage(null, null, 0, mm.LogicalCallContext, (IMethodCallMessage)msg);
            }
            Async asyncNext = _asyncNext;
            OperationTypeInfo operationTypeInfoNext = _operationTypeInfoNext;

            //out ref�Ή�
            object[] args;
            Func<object>[] refoutArgsFunc;
            AdjustRefOutArgs(method, mm.Args, out args, out refoutArgsFunc, ref asyncNext, ref operationTypeInfoNext);

            //�ÓI�ȏ��Ń^�C�v�������邩
            //������_operationTypeInfo���ݒ肳��Ă���΁A�����D�悷��
            if (_operationTypeInfoNext == null)
            {
            }

            //�Ăяo��            
            string invokeName = GetInvokeName(method);
            var returnedAppVal = Invoke(method, invokeName, args, ref asyncNext, ref operationTypeInfoNext);

            //null�ɂ���Ă�����null������B�����łȂ���΁A���񎝉z��
            if (asyncNext == null)
            {
                _asyncNext = null;
            }
            if (operationTypeInfoNext == null)
            {
                _operationTypeInfoNext = null;
            }

            //�߂�l��out,ref�̏���
            object objReturn = ToReturnObject(returnedAppVal, method.ReturnParameter);
            var refoutArgs = refoutArgsFunc.Select(e => e()).ToArray();
            return new ReturnMessage(objReturn, refoutArgs, refoutArgs.Length, mm.LogicalCallContext, (IMethodCallMessage)msg);
        }

        private string GetInvokeName(MethodInfo method)
        {
            //�z��Ƃ��̑���[]�A�N�Z�X�̍������z�����鏈��
            string invokeName = method.Name;
            if (invokeName == "get_Item")
            {
                return "[" + GetCommas(method.GetParameters().Length - 1) + "]";
            }
            else if (invokeName == "set_Item")
            {
                return "[" + GetCommas(method.GetParameters().Length - 2) + "]";
            }
            //�v���p�e�B�[���w���������̂��t�B�[���h�ł���ꍇ�̑Ή�
            else if (invokeName.IndexOf("get_") == 0 ||
                    invokeName.IndexOf("set_") == 0)
            {
                return invokeName.Substring(4);
            }
            return invokeName;
        }

        private string GetCommas(int count)
        {
            StringBuilder b = new StringBuilder();
            for (int i = 0; i < count; i++)
            {
                b.Append(",");
            }
            return b.ToString();
        }

        protected abstract AppVar Invoke(MethodInfo method, string name, object[] args, ref Async async, ref OperationTypeInfo info);

        private void AdjustRefOutArgs(MethodInfo method, object[] src, out object[] args, out  Func<object>[] refoutArgsFunc, ref Async async, ref OperationTypeInfo typeInfo)
        {
            List<object> listArgs = new List<object>();
            refoutArgsFunc = new Func<object>[src.Length];
            var parameters = method.GetParameters();
            for (int i = 0; i < parameters.Length; i++)
            {
                if (parameters[i].ParameterType.IsByRef)
                {
                    object arg;
                    Func<object> refoutFunc;
                    AdjustRefOutArgs(parameters[i].ParameterType.GetElementType(), src[i], out arg, out refoutFunc);
                    listArgs.Add(arg);
                    refoutArgsFunc[i] = refoutFunc;
                }
                else
                {
                    object srcObj = src[i];
                    refoutArgsFunc[i] = () => srcObj;
                    Async checkAsync = srcObj as Async;
                    if (checkAsync != null)
                    {
                        if (async != null)
                        {
                            throw new NotSupportedException("����̌Ăяo���ɑ΂��āA����Async�͎w�肳��Ă��܂��B");
                        }
                        async = checkAsync;
                        continue;
                    }

                    OperationTypeInfo checkInfo = srcObj as OperationTypeInfo;
                    if (checkInfo != null)
                    {
                        if (typeInfo != null)
                        {
                            throw new NotSupportedException("����̌Ăяo���ɑ΂��āA����OperationTypeInfo�͎w�肳��Ă��܂��B");
                        }
                        typeInfo = checkInfo;
                        continue;
                    }
                    listArgs.Add(srcObj);
                }
            }
            args = listArgs.ToArray();
        }

        private void AdjustRefOutArgs(Type type, object src, out object arg, out Func<object> refoutFunc)
        {
            //@@@ IInstance���H

            //@@@dynamic�Ή�

            if (type.IsInterface)
            {
                if (src == null)
                {
                    AppVar appVar = App.Dim();
                    arg = appVar;
                    refoutFunc = () => WrapChildAppVar(type, appVar);
                }
                else
                {
                    IAppVarOwner appVarOwner = src as IAppVarOwner;
                    if (appVarOwner != null)
                    {
                        arg = src;
                        refoutFunc = () => src;
                    }
                    else
                    {
                        AppVar appVar = App.Dim();
                        arg = appVar;
                        refoutFunc = () => WrapChildAppVar(type, appVar);
                    }
                }
            }
            else if (type == typeof(AppVar))
            {
                if (src == null)
                {
                    AppVar appVar = App.Dim();
                    arg = appVar;
                    refoutFunc = () => appVar;
                }
                else
                {
                    arg = src;
                    refoutFunc = () => src;
                }
            }
            else
            {
                AppVar appVar = App.Copy(src);
                arg = appVar;
                refoutFunc = () => appVar.Core;
            }
        }

        private static object ToReturnObject(AppVar returnedAppVal, ParameterInfo parameterInfo)
        {
            if (parameterInfo.ParameterType == typeof(void))
            {
                return null;
            }
            else if (parameterInfo.ParameterType == typeof(AppVar))
            {
                return returnedAppVal;
            }
            else if (parameterInfo.ParameterType.IsInterface)
            {
                return WrapChildAppVar(parameterInfo.ParameterType, returnedAppVal);
            }
            else if (parameterInfo.GetCustomAttributes(false).Any(o => o is DynamicAttribute))
            {
                return returnedAppVal.Dynamic();
            }
            else
            {
                return returnedAppVal.Core;
            }
        }

        private static object WrapChildAppVar(Type type, AppVar ret)
        {
            var friendlyProxyType = typeof (FriendlyProxyInstance<>).MakeGenericType(type);
            dynamic friendlyProxy = Activator.CreateInstance(friendlyProxyType, new object[] {ret});
            return friendlyProxy.GetTransparentProxy();
        }


        protected static FriendlyOperation GetFriendlyOperation(dynamic target, string name, Async async, OperationTypeInfo typeInfo)
        {
            if (async != null && typeInfo != null)
            {
                return target[name, typeInfo, async];
            }
            else if (async != null)
            {
                return target[name, async];
            }
            else if (typeInfo != null)
            {
                return target[name, typeInfo];
            }
            return target[name];
        }
    }
}