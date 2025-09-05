using System;
using System.Collections.Generic;
using System.DirectoryServices.ActiveDirectory;
using SnaffCore.Config;
using static SnaffCore.Config.Options;

namespace SnaffCore.ActiveDirectory
{
    public class AdData
    {
        private Options Options { get; set; }
        // Removed dependency on non-existent Ldap class
        private DirectorySearch _directorySearch;

        public AdData()
        {
            Options = MyOptions;
            _directorySearch = new DirectorySearch();
        }

        public List<string> GetDomainUsers()
        {
            string domain;
            try
            {
                domain = Domain.GetComputerDomain().ToString();
            }
            catch (Exception)
            {
                domain = Environment.UserDomainName;
            }

            var principalContext = new System.DirectoryServices.AccountManagement.PrincipalContext(
                System.DirectoryServices.AccountManagement.ContextType.Domain, domain);
            var userPrincipal =
                new System.DirectoryServices.AccountManagement.UserPrincipal(principalContext);
            var searcher = new System.DirectoryServices.AccountManagement.PrincipalSearcher(userPrincipal);

            List<string> userList = new List<string>();

            foreach (var result in searcher.FindAll())
            {
                if (result.SamAccountName.Length >= Options.DomainUserMinLen)
                {
                    foreach (var format in Options.DomainUserNameFormats)
                    {
                        switch (format)
                        {
                            case DomainUserNamesFormat.sAMAccountName:
                                userList.Add(result.SamAccountName);
                                break;
                            case DomainUserNamesFormat.NetBIOS:
                                userList.Add(domain + "\\" + result.SamAccountName);
                                break;
                            case DomainUserNamesFormat.UPN:
                                userList.Add(result.UserPrincipalName);
                                break;
                        }
                    }
                }
            }
            searcher.Dispose();
            return userList;
        }
    }
}

