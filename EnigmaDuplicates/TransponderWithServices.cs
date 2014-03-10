using System;
using System.Collections.Generic;
using System.Globalization;
using System.Linq;
using System.Text;

namespace Krkadoni.EnigmaDuplicates
{
    using System;
    using System.Collections.Generic;
    using System.ComponentModel;
    using Krkadoni.EnigmaSettings.Interfaces;

    public class TransponderWithServices
    {

        private readonly List<ServiceWithBoquets> _services;

        internal readonly ITransponder Transponder;
        public List<ServiceWithBoquets> Services
        {
            get { return _services; }
        }

        public TransponderWithServices(ITransponder transponder, IEnumerable<IService> services)
        {
            if ((transponder == null))
                throw new ArgumentNullException("Transponder");
            this.Transponder = transponder;
            _services = new List<ServiceWithBoquets>();
            services.ToList().ForEach(s => _services.Add(new ServiceWithBoquets(s)));
        }

        public string Frequency
        {
            get { return Transponder.Frequency; }
        }

        [DisplayName("Namespace")]
        public string NameSpc
        {
            get { return Transponder.NameSpc; }
        }

        public string TSID
        {
            get { return Transponder.TSID; }
        }

        public string NID
        {
            get { return Transponder.NID; }
        }

        public string Description
        {
            get
            {
                if (Transponder is ITransponderDVBS)
                {
                    return string.Format("Transponder: {0} {1}", Transponder.Frequency, ((ITransponderDVBS)Transponder).PolarizationType.ToString().Substring(0, 1));
                }
                else
                {
                    return string.Format("Transponder: {0} ", Transponder.Frequency);
                }
            }
        }

    }
}
