using BaseCore.Common;
using BaseCore.Entities.Audit;
using System;
using System.Collections.Generic;

namespace BaseCore.Entities
{
    public partial class Role : Entity, IAuditable
    {
        public Role()
        {
            RoleModule = new HashSet<Module>();
            UserRole = new HashSet<UserRole>();
        }

        public Guid Guid { get; set; }
        public string Name { get; set; }
        public string Description { get; set; }
        public string CreatedBy { get; set; }
        public DateTime Created { get; set; } = DateTime.Now;
        public string ModifiedBy { get; set; }
        public DateTime Modified { get; set; }
        public bool IsDeleted { get; set; }
        public bool IsActive { get; set; }
        public int RoleType { get; set; }
        public virtual ICollection<Module> RoleModule { get; set; }
        public virtual ICollection<UserRole> UserRole { get; set; }
    }
}
