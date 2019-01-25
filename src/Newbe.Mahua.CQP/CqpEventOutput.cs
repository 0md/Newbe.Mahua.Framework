using System;

namespace Newbe.Mahua.CQP
{
    public abstract class CqpEventOutput : IEventOutput
    {
        public MahuaPlatform Platform { get; } = MahuaPlatform.Cqp;
        public DateTimeOffset CreateTime { get; } = DateTimeOffset.UtcNow;
        public abstract string TypeCode { get; }
    }
}
