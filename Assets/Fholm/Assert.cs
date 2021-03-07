/*
The MIT License (MIT)
Copyright (c) 2021 Fredrik Holmstrom
Permission is hereby granted, free of charge, to any person obtaining a copy
of this software and associated documentation files (the "Software"), to deal
in the Software without restriction, including without limitation the rights
to use, copy, modify, merge, publish, distribute, sublicense, and/or sell
copies of the Software, and to permit persons to whom the Software is
furnished to do so, subject to the following conditions:
The above copyright notice and this permission notice shall be included in
all copies or substantial portions of the Software.
THE SOFTWARE IS PROVIDED "AS IS", WITHOUT WARRANTY OF ANY KIND, EXPRESS OR
IMPLIED, INCLUDING BUT NOT LIMITED TO THE WARRANTIES OF MERCHANTABILITY,
FITNESS FOR A PARTICULAR PURPOSE AND NONINFRINGEMENT. IN NO EVENT SHALL THE
AUTHORS OR COPYRIGHT HOLDERS BE LIABLE FOR ANY CLAIM, DAMAGES OR OTHER
LIABILITY, WHETHER IN AN ACTION OF CONTRACT, TORT OR OTHERWISE, ARISING FROM,
OUT OF OR IN CONNECTION WITH THE SOFTWARE OR THE USE OR OTHER DEALINGS IN
THE SOFTWARE.
*/

using System;
using System.Diagnostics;

namespace Fholm
{
    public class AssertException : Exception
    {
        public AssertException()
        {
        }

        public AssertException(string msg) : base(msg)
        {
        }
    }

    // Caelan: Custom assert class found online, simple and effective.
    // Anything with Conditional attributes will remove themselves automatically on Release.
    // We can put these into the code as verification checks during development,
    // but any the calls to them will be omitted when pushed to QA/Prod.
    // Thus we don't need to worry about wrapping debug checks in #if DEBUG blocks.
    public static class Assert
    {
        [Conditional("DEBUG")]
        public static void Fail()
        {
            throw new AssertException();
        }

        [Conditional("DEBUG")]
        public static void Fail(string error)
        {
            throw new AssertException(error);
        }

        [Conditional("DEBUG")]
        public static void Fail(string format, params object[] args)
        {
            throw new AssertException(string.Format(format, args));
        }

        [Conditional("DEBUG")]
        public static void Check(object condition)
        {
            if (condition == null)
            {
                throw new AssertException();
            }
        }

        [Conditional("DEBUG")]
        public static unsafe void Check(void* condition)
        {
            if (condition == null)
            {
                throw new AssertException();
            }
        }

        [Conditional("DEBUG")]
        public static void Check(bool condition)
        {
            if (!condition)
            {
                throw new AssertException();
            }
        }

        [Conditional("DEBUG")]
        public static void Check<T0>(bool condition, T0 arg0)
        {
            if (!condition)
            {
                throw new AssertException($"arg0:{arg0}");
            }
        }

        [Conditional("DEBUG")]
        public static void Check<T0, T1>(bool condition, T0 arg0, T1 arg1)
        {
            if (!condition)
            {
                throw new AssertException($"arg0:{arg0} arg1:{arg1}");
            }
        }

        [Conditional("DEBUG")]
        public static void Check<T0, T1, T2>(bool condition, T0 arg0, T1 arg1, T2 arg2)
        {
            if (!condition)
            {
                throw new AssertException($"arg0:{arg0} arg1:{arg1} arg2:{arg2}");
            }
        }

        [Conditional("DEBUG")]
        public static void Check(bool condition, string error)
        {
            if (!condition)
            {
                throw new AssertException(error);
            }
        }

        [Conditional("DEBUG")]
        public static void Check(bool condition, string format, params object[] args)
        {
            if (!condition)
            {
                throw new AssertException(string.Format(format, args));
            }
        }

        // `Always` prefixed methods don't have the Conditional attributes and will be present on QA/Prod.

        public static void AlwaysFail()
        {
            throw new AssertException();
        }

        public static void AlwaysFail(string error)
        {
            throw new AssertException(error);
        }

        public static void AlwaysFail(object error)
        {
            throw new AssertException(error.ToString());
        }

        public static void Always(bool condition)
        {
            if (!condition)
            {
                throw new AssertException();
            }
        }

        public static void Always(bool condition, string error)
        {
            if (!condition)
            {
                throw new AssertException(error);
            }
        }

        public static void Always(bool condition, string format, params object[] args)
        {
            if (!condition)
            {
                throw new AssertException(string.Format(format, args));
            }
        }
    }
}