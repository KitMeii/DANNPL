using MongoDB.Bson;
using MongoDB.Bson.Serialization.Attributes;
using System.Collections.Generic;

namespace BaseCore.DTO.AuthPlatform
{
    public class ModuleDto
    {
        [BsonRepresentation(BsonType.ObjectId)]
        public string Id { get; set; }
        public string Name { get; set; }
        public string Description { get; set; }
        public bool IsDeleted { get; set; }
        public bool IsActive { get; set; }
        public bool IsChecked { get; set; }
        public ICollection<FunctionDto> ModuleFunction { get; set; }
    }

    public class ModuleStatusParam {
        public string Id { get; set; }
        public bool IsActive { get; set; }
    }
}
