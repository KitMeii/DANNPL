using MongoDB.Bson;
using MongoDB.Bson.Serialization.Attributes;
using System.Collections.Generic;
using System.ComponentModel.DataAnnotations;

namespace BaseCore.DTO.AuthPlatform
{
    public class ModuleFunctionDto
    {
        
        public int ModuleFunctionId { get; set; }

        [Required]
        [BsonRepresentation(BsonType.ObjectId)]
        public string FunctionId { get; set; }
        [Required]
        [BsonRepresentation(BsonType.ObjectId)]
        public string ModuleId { get; set; }
        public string ModuleName { get; set; }
        public string FunctionName { get; set; }
        public bool IsActive { get; set; }
        public List<RoleModuleFunctionDto> RoleModuleFunctions { get; set; }
    }
}
 