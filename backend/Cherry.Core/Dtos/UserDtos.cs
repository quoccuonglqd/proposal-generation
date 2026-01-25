using System;
using System.Collections.Generic;

namespace Cherry.Core.Dtos
{
    public record UserDto(Guid Id, string Email, string DisplayName, IList<string> Roles);
    public record UpdateUserRolesRequest(IList<string> Roles);
}
