﻿using Newbe.Mahua.Messages;

namespace Newbe.Mahua.CQP.Messages
{
    public class GroupMessageFlowFactory : IGroupMessageFlowFactory
    {
        private readonly IGroupMessageStep _groupMessageStep;
        private readonly IMessage _message;

        public GroupMessageFlowFactory(
            IGroupMessageStep groupMessageStep,
            IMessage message)
        {
            _groupMessageStep = groupMessageStep;
            _message = message;
        }

        public IGroupMessageStep Begin(string @group)
        {
            _message.Target = group;
            return _groupMessageStep;
        }
    }
}
