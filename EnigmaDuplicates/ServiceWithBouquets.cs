using System;
using System.Collections.Generic;
using System.ComponentModel;
using System.Linq;
using Krkadoni.EnigmaSettings.Interfaces;

namespace Krkadoni.EnigmaDuplicates
{

    public class ServiceWithBoquets
    {

        internal readonly IService Service;
        public ServiceWithBoquets(IService service)
        {
            if ((service == null))
                throw new ArgumentNullException("Service");
            this.Service = service;
            Bouquets = new List<IBouquet>();
        }

        public List<IBouquet> Bouquets { get; set; }

        public string Name
        {
            get { return Service.Name; }
        }

        [DisplayName("Type")]
        public string ServiceType
        {
            get { return Service.ServiceType.ToString(); }
        }

        public string Provider
        {
            get { return Service.FlagList.Where(x => x.FlagType == Enums.FlagType.P).Select(x => x.FlagValue).SingleOrDefault(); }
        }

        [DisplayName("Prog. Number")]
        public string ProgNumber
        {
            get { return Service.ProgNumber; }
        }

        public string Flags
        {
            get { return Service.Flags; }
        }

        public string Description
        {
            get
            {
                if (Service.FlagList.Where(x => x.FlagType == Enums.FlagType.P).FirstOrDefault() != null)
                {
                    if (Bouquets.Any())
                    {
                        return String.Format("{0}    ({1}) / ({2})", Service.Name, Service.FlagList.Where(x => x.FlagType == Enums.FlagType.P).First().FlagValue, Bouquets.Count);
                    }
                    else
                    {
                        return String.Format("{0}    ({1})", Service.Name, Service.FlagList.Where(x => x.FlagType == Enums.FlagType.P).First().FlagValue);
                    }                  
                }
                else
                {
                    if (Bouquets.Any())
                    {
                         return  String.Format("{0}    ({1})",  Service.Name, Bouquets.Count);
                    }
                    else
                {
                    return Service.Name;
                }
                   
                }
            }
        }
    }
}
