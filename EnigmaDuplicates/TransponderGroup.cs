using System.Collections.Generic;
using Krkadoni.EnigmaSettings;
using Krkadoni.EnigmaSettings.Interfaces;

namespace Krkadoni.EnigmaDuplicates
{

    public class TransponderGroup
    {

        public string Description { get; set; }

        public string Position
        {
            get
            {
                if (Satellite == null)
                {
                    return string.Empty;
                }
                else
                {
                    return Satellite.PositionString;
                }
            }
        }

        public List<TransponderWithServices> Transponders { get; set; }

        internal IXmlSatellite Satellite;
    }
}

