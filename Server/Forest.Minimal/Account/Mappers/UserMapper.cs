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
    [MapperIgnoreTarget(nameof(Details.PhotoId))]
    [MapperIgnoreTarget(nameof(Details.BannerId))]
    public static partial Details ToDetails(SignUpCommand command);
    [MapperIgnoreSource(nameof(SignUpCommand.Banner))]
    [MapperIgnoreSource(nameof(SignUpCommand.Photo))]
    [MapperIgnoreSource(nameof(SignUpCommand.UserName))]
    [MapperIgnoreSource(nameof(SignUpCommand.FirstName))]
    [MapperIgnoreSource(nameof(SignUpCommand.LastName))]
    public static partial Security ToSecurity(SignUpCommand command);
    [MapperIgnoreSource(nameof(UserModel.Key))]
    [MapperIgnoreSource(nameof(UserModel.IsEnable))]
    [MapperIgnoreSource(nameof(UserModel.Tokens))]
    [MapperIgnoreSource(nameof(UserModel.Register))]
    [MapProperty(nameof(UserModel.UserName), nameof(Profile.UserName))]
    [MapProperty(nameof(UserModel.Details.FirstName), nameof(Profile.FirstName))]
    [MapProperty(nameof(UserModel.Details.LastName), nameof(Profile.LastName))]
    [MapProperty(nameof(UserModel.Security.Email), nameof(Profile.Email))]
    [MapProperty(nameof(UserModel.Details.PhotoId), nameof(Profile.PhotoId))]
    [MapProperty(nameof(UserModel.Details.BannerId), nameof(Profile.BannerId))]
    public static partial Profile ToProfile(UserModel model);
}