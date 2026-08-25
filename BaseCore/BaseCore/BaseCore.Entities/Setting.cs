using System;
using System.Collections.Generic;
using BaseCore.Common;
using BaseCore.Entities.Audit;

namespace BaseCore.Entities
{
    public class Setting : Entity, IAuditable
    {
        public bool IsOnline { get; set; }
        public bool IsTwoCamera { get; set; }
        public string JistiDomain { get; set; }
        public string Name { get; set; }
        public string CreatedBy { get; set; }
        public DateTime Created { get; set; } = DateTime.Now;
        public string ModifiedBy { get; set; }
        public DateTime Modified { get; set; }
        public bool IsDeleted { get; set; }
        public bool IsActive { get; set; }
        public string JistiDomainInternet { get; set; }
    }
}
