using Forest.Minimal.Account.Handlers;
using Forest.Minimal.Account.Models;
using Riok.Mapperly.Abstractions;

namespace Forest.Minimal.Account.Mappers;

[Mapper(EnumMappingStrategy = EnumMappingStrategy.ByName, EnumMappingIgnoreCase = true, EnabledConversions = MappingConversionType.All)]
public static partial class UserMapper
{
    [MapPropertyFromSource(nameof(UserModel.Security), Use = nameof(ToSecurity))]
    [MapPropertyFromSource(nameof(UserModel.Details), Use = nameof(ToDetails))]
    [MapperIgnoreTarget(nameof(UserModel.Key))]
    [MapperIgnoreTarget(nameof(UserModel.Tokens))]
    [MapperIgnoreTarget(nameof(UserModel.IsEnable))]
    [MapperIgnoreTarget(nameof(UserModel.Register))]
    public static partial UserModel ToUserModel(SignUpCommand source);
    [MapperIgnoreSource(nameof(SignUpCommand.Banner))]
    [MapperIgnoreSource(nameof(SignUpCommand.Photo))]
    [MapperIgnoreSource(nameof(SignUpCommand.UserName))]
    [MapperIgnoreSource(nameof(SignUpCommand.Email))]
    [MapperIgnoreSource(nameof(SignUpCommand.Password))]
    public static partial Details ToDetails(SignUpCommand command);
    [MapperIgnoreSource(nameof(SignUpCommand.Banner))]
    [MapperIgnoreSource(nameof(SignUpCommand.Photo))]
    [MapperIgnoreSource(nameof(SignUpCommand.UserName))]
    [MapperIgnoreSource(nameof(SignUpCommand.FirstName))]
    [MapperIgnoreSource(nameof(SignUpCommand.LastName))]
    public static partial Security ToSecurity(SignUpCommand command);
}