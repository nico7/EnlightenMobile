﻿using System;

namespace EnlightenMobile.Models
{
    // encapsulate battery processing
    public class Battery
    {
        ushort raw;
        byte rawLevel;
        byte rawState;
        double level;
        
        bool charging;

        Logger logger = Logger.getInstance();

        public void parse(byte[] response)
        {
            if (response is null)
            {
                logger.error("Battery: no response");
                return;
            }

            if (response.Length != 2)
            {
                logger.error("Battery: invalid response");
                return;
            }

            ushort raw = ParseData.toUInt16(response, 0);
            this.raw = raw; // store for debugging, as toString() outputs this

            // reversed from SiG-290
            rawLevel = (byte)((raw & 0xff00) >> 8);
            rawState = (byte)(raw & 0xff);

            level = (double)rawLevel;
            
            charging = (rawState & 1) == 1;

            logger.debug($"Battery.parse: {this}");
        }

        override public string ToString()
        {
            logger.debug("Battery: raw 0x{0:x4} (lvl {1}, st 0x{2:x2}) = {3:f2}", raw, rawLevel, rawState, level);
            return string.Format("Battery {0} ({1}%)", charging ? "charging" : "discharging", (int)Math.Round(level));
        }
    }
}