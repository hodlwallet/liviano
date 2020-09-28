//
// Version.cs
//
// Copyright (c) 2019 HODL Wallet
//
// Permission is hereby granted, free of charge, to any person obtaining a copy
// of this software and associated documentation files (the "Software"), to deal
// in the Software without restriction, including without limitation the rights
// to use, copy, modify, merge, publish, distribute, sublicense, and/or sell
// copies of the Software, and to permit persons to whom the Software is
// furnished to do so, subject to the following conditions:
//
// The above copyright notice and this permission notice shall be included in
// all copies or substantial portions of the Software.
//
// THE SOFTWARE IS PROVIDED "AS IS", WITHOUT WARRANTY OF ANY KIND, EXPRESS OR
// IMPLIED, INCLUDING BUT NOT LIMITED TO THE WARRANTIES OF MERCHANTABILITY,
// FITNESS FOR A PARTICULAR PURPOSE AND NONINFRINGEMENT. IN NO EVENT SHALL THE
// AUTHORS OR COPYRIGHT HOLDERS BE LIABLE FOR ANY CLAIM, DAMAGES OR OTHER
// LIABILITY, WHETHER IN AN ACTION OF CONTRACT, TORT OR OTHERWISE, ARISING FROM,
// OUT OF OR IN CONNECTION WITH THE SOFTWARE OR THE USE OR OTHER DEALINGS IN
// THE SOFTWARE.
using System.Reflection;

namespace Liviano
{
    public sealed class Version
    {
        public static string Number
        {
            get
            {
                // Version numbers on .NET are the following:
                // {Major}.{Minor}.{Revision}.{Build}

                // If we have no revision or build then the version only has {Major}.{Minor}
                if (AssemblyVersion.Revision == 0 && AssemblyVersion.Build == 0)
                    return AssemblyVersion.ToString(2);

                // If we have a revision but not a build number we skip it.
                if (AssemblyVersion.Build == 0)
                    return AssemblyVersion.ToString(3);

                // Show all 4 fields
                return AssemblyVersion.ToString();
            }
        }

        public static string UserAgent
        {
            get
            {
                return $"/{Name}:{Number}/";
            }
        }

        public static string ElectrumUserAgent
        {
            get
            {
                return $"{Slug}/{Number}";
            }
        }

        public static System.Version ToSystemVersion()
        {
            return new System.Version(Number);
        }

        public static new string ToString()
        {
            return $"{Name} ({Number})";
        }

        public static string Name
        {
            get
            {
                return CurrentAssembly.GetName().Name;
            }
        }

        public static string Slug
        {
            get
            {
                return CurrentAssembly.GetName().Name.ToLower();
            }
        }

        static System.Version AssemblyVersion
        {
            get
            {
                return CurrentAssembly.GetName().Version;
            }
        }

        static Assembly CurrentAssembly
        {
            get
            {
                return typeof(Version).Assembly;
            }
        }
    }
}
