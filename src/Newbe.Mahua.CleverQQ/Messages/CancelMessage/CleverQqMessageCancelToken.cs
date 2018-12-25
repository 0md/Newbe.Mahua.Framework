using Newbe.Mahua.Messages.CancelMessage;

namespace Newbe.Mahua.CleverQQ.Messages.CancelMessage
{
    public class CleverQqMessageCancelToken : IMessageCancelToken
    {
        public static readonly IMessageCancelToken EmptyActionToken = new CleverQqMessageCancelToken();

        private CleverQqMessageCancelToken()
        {
        }


        public void Cancel()
        {
            // 不支持发出消息的撤回 
        }
    }
}