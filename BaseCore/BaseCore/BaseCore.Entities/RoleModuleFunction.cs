
using BaseCore.Common;
using BaseCore.Entities.Audit;
using System;

namespace BaseCore.Entities
{
    public partial class RoleModuleFunction : Entity, IAuditable
    {
        public Guid Guid { get; set; }
       
        public string RoleId { get; set; }
     
        public string ModuleFunctionId { get; set; }
        public string CreatedBy { get; set; }
        public DateTime Created { get; set; } = DateTime.Now;
        public string ModifiedBy { get; set; }
        public DateTime Modified { get; set; }
        public bool IsDeleted { get; set; }
        public bool IsActive { get; set; }
        public virtual Role Role { get; set; }
        public virtual ModuleFunction ModuleFunction { get; set; }

    }
}
