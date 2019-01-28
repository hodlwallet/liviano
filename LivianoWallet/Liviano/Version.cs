using System;
using System.Collections.Generic;
using System.Reflection;
using System.Text;

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

        public static string Name
        {
            get
            {
                return CurrentAssembly.GetName().Name;
            }
        }

        private static System.Version AssemblyVersion
        {
            get
            {
                return CurrentAssembly.GetName().Version;
            }
        }

        private static Assembly CurrentAssembly
        {
            get
            {
                return typeof(Version).Assembly;
            }
        }
    }
}
