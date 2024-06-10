using AssettoServer.Server.Plugin;
using Autofac;

namespace NordschleifeTrackdayPlugin;

public class NordschleifeTrackdayModule : AssettoServerModule<NordschleifeTrackdayConfiguration>
{
    protected override void Load(ContainerBuilder builder)
    {
        builder.RegisterType<NordschleifeTrackdayPlugin>().AsSelf().AutoActivate().SingleInstance();
    }
}
