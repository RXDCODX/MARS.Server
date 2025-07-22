//using HuTao.NET;
//using MARS.Server.Services.ServiceManager;

//namespace MARS.Server.Services.Honkai;

//public class DailyMarkRedeemService(
//    IOptions<HoyolabConfiguration> options,
//    ILogger<DailyMarkRedeemService> logger,
//    IHostApplicationLifetime lifetime
//) : ManagedServiceBase(logger)
//{
//    public override string ServiceName { get; }
//    public override string DisplayName { get; }
//    public override string Description { get; }
//    public override bool IsServiceActive { get; set; }
//    public readonly HoyolabConfiguration Configuration = options.Value;
//    public HuTaoClient? HoyolabClient { get; private set; }

//    public override Task StartAsync(CancellationToken cancellationToken = default)
//    {
//        lifetime.ApplicationStarted.Register(() =>
//        {
//            HoyolabClient = HuTaoClient.Create(new CookieV2()
//            {
//                LTokenV2 = Configuration.Ltoken_v2,
//                LtMidV2 = Configuration.Ltmid_v2,
//                LtUidV2 = Configuration.Ltuid_v2
//            });

//            HoyolabClient.StarRail.
//        })

//        return base.StartAsync(cancellationToken);
//    }
//}
