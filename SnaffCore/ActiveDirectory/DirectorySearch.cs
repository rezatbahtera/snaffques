using SnaffCore.Config;
using System;
using System.Collections.Generic;
using System.DirectoryServices;
using System.DirectoryServices.ActiveDirectory;
using static SnaffCore.Config.Options;

namespace SnaffCore.ActiveDirectory
{
    public class DirectorySearch
    {
        private readonly Options _options;

        public DirectorySearch()
        {
            _options = MyOptions;
        }

        public List<string> GetComputers(string ldapFilter)
        {
            var computers = new List<string>();
            try
            {
                var domain = GetCurrentDomain();
                using (var de = new DirectoryEntry($"LDAP://{domain}"))
                {
                    using (var ds = new System.DirectoryServices.DirectorySearcher(de, ldapFilter))
                    {
                        ds.PageSize = 1000;
                        ds.PropertiesToLoad.Add("dNSHostName");

                        foreach (SearchResult result in ds.FindAll())
                        {
                            if (result.Properties.Contains("dNSHostName"))
                            {
                                computers.Add(result.Properties["dNSHostName"][0].ToString());
                            }
                        }
                    }
                }
            }
            catch (Exception e)
            {
                // Mute exception for now to avoid console spam on permission errors
                // Console.WriteLine($"Error getting computers from AD: {e.Message}");
            }
            return computers;
        }

        private static string GetCurrentDomain()
        {
            try
            {
                return Domain.GetCurrentDomain().Name;
            }
            catch (ActiveDirectoryObjectNotFoundException)
            {
                return Environment.GetEnvironmentVariable("USERDNSDOMAIN");
            }
        }
    }
}

