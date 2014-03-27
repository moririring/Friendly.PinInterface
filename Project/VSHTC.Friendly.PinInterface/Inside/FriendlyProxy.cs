using System;
using System.Reflection;
using System.Runtime.Remoting.Messaging;
using System.Runtime.Remoting.Proxies;
using Codeer.Friendly;

namespace VSHTC.Friendly.PinInterface.Inside
{
    abstract class FriendlyProxy<TInterface> : RealProxy
    {
        Async _asyncNext;
        OperationTypeInfo _operationTypeInfoNext;

        protected AppFriend App { get; private set; }

        public FriendlyProxy(AppFriend app)
            : base(typeof(TInterface)) 
        {
            App = app;
        }

        public override IMessage Invoke(IMessage msg)
        {
            var mm = msg as IMethodMessage;
            var method = (MethodInfo)mm.MethodBase;

            //����C���^�[�t�F�C�X�Ή�
            object retunObject;
            if (InterfacesSpec.TryExecuteSpecialInterface<TInterface>(this as IAppVarOwner, method, mm.Args, 
                ref _asyncNext, ref _operationTypeInfoNext, out retunObject))
            {
                return new ReturnMessage(retunObject, null, 0, mm.LogicalCallContext, (IMethodCallMessage)msg);
            }
            bool isAsyunc = _asyncNext != null;

            //��������
            ArgumentResolver args = new ArgumentResolver(App, method, isAsyunc, mm.Args);

            //�Ăяo��            
            string invokeName = FriendlyInvokeSpec.GetInvokeName(method);
            var returnedAppVal = Invoke(method, invokeName, args.InvokeArguments, ref _asyncNext, ref _operationTypeInfoNext);

            //�߂�l��out,ref�̏���
            object objReturn = ReturnObjectResolver.Resolve(isAsyunc, returnedAppVal, method.ReturnParameter);
            var refoutArgs = args.GetRefOutArgs();
            return new ReturnMessage(objReturn, refoutArgs, refoutArgs.Length, mm.LogicalCallContext, (IMethodCallMessage)msg);
        }

        protected abstract AppVar Invoke(MethodInfo method, string name, object[] args, ref Async async, ref OperationTypeInfo info);
    }
}