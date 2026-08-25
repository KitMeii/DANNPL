
using MongoDB.Bson;
using MongoDB.Bson.Serialization.Attributes;

namespace BaseCore.DTO.AuthPlatform
{
    public class RoleModuleFunctionDto
    {
        [BsonRepresentation(BsonType.ObjectId)]
        public string Id { get; set; }
        public string ModuleFunctionId { get; set; }
        public string FunctionId { get; set; }
        public string RoleId { get; set; }
        public string ModuleId { get; set; }
        public bool IsActive { get; set; }
    }
}
