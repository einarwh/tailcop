using System;

namespace Tailcop
{
    class EvalStack
    {
        private const char YesThis = '1';

        private const char NotThis = '0';

        private readonly string _;

        private EvalStack(string s)
        {
            _ = s;
        }

        public EvalStack() : this("") {}

        public EvalStack Pop()
        {
            if (IsEmpty)
            {
                throw new Exception("Cannot Pop an empty stack.");
            }
            return new EvalStack(_.Substring(1));
        }

        public EvalStack Push(bool b)
        {
            char c = b ? YesThis : NotThis;
            return new EvalStack(c + _);
        }

        public bool Peek()
        {
            if (IsEmpty)
            {
                throw new Exception("Cannot Peek an empty stack.");
            }
            return _[0] == YesThis;
        }

        public bool IsEmpty
        {
            get
            {
                return _.Length == 0;
            }
        }

        public int Depth
        {
            get
            {
                return _.Length; 
            }
        }

        public override bool Equals(object that)
        {
            return Equals(that as EvalStack);
        }

        public bool Equals(EvalStack that)
        {
            return _.Equals(that._);
        }

        public override int GetHashCode()
        {
            return (_ != null ? _.GetHashCode() : 0);
        }

        public override string ToString()
        {
            return "[" + _ + "]";
        }
    }
}
