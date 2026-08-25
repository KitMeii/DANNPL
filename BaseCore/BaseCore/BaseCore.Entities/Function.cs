using BaseCore.Common;
using BaseCore.Entities.Audit;
using System;
using System.Collections.Generic;

namespace BaseCore.Entities
{
    public partial class Function: Entity, IAuditable
    {
        public Function()
        {
            ModuleFunction = new HashSet<ModuleFunction>();
        }

        public string Name { get; set; }
        public string Description { get; set; }
        public string CreatedBy { get; set; }
        public DateTime Created { get; set; } = DateTime.Now;
        public string ModifiedBy { get; set; }
        public DateTime Modified { get; set; }
        public bool IsDeleted { get; set; }
        public bool IsActive { get; set; }
        public bool IsChecked { get; set; }

        public virtual ICollection<ModuleFunction> ModuleFunction { get; set; }
    }
}
