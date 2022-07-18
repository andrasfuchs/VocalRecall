using System;
using System.Collections.Generic;
using System.Linq;
using System.Web;

namespace VocalRecallService
{
    public static class EntityCache
    {
        private static Culture[] cultures;

        public static Culture[] Cultures
        {
            get
            {
                if (cultures == null)
                {
					VocalRecallEntities entities = new VocalRecallEntities();
                    cultures = entities.Cultures.ToArray();
                    entities.Dispose();
                }

                return cultures;
            }
        }
    }
}