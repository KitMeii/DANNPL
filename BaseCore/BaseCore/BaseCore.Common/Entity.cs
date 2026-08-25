
using System;

namespace BaseCore.Common
{
    public class Entity
    {
       
        public string Id { get; set; } = Guid.NewGuid().ToString();

        public DateTime CreatedDateTime { get; set; } = DateTime.UtcNow;
        public string CreatedUser { get; set; }
    }
}
