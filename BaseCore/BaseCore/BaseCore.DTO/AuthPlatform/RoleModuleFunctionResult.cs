using System;
using System.Collections.Generic;

namespace BaseCore.DTO.AuthPlatform
{
    public class RoleModuleFunctionResult
    {
        public List<ModuleFunctionDto> RecordModules { get; set; }
        public List<FunctionDto> ColumnFunctions { get; set; }
    }
}
