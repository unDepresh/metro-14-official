using System.Linq;
using Content.Server.Administration;
using Content.Server.Administration.Managers;
using Content.Server.Database;
using Content.Shared.Administration;
using Robust.Server.Player;
using Robust.Shared.Console;
using Robust.Shared.Network;

namespace Content.Server._Metro14.SponsorSystem;

/// <summary>
/// Класс-обработчик команды для добавления игрока в БД спонсоров.
/// </summary>
[AdminCommand(AdminFlags.Sponsor)]
public sealed class SponsorSystemAddCommand : LocalizedCommands
{
    [Dependency] private readonly IServerDbManager _dbManager = default!;
    [Dependency] private readonly IPlayerManager _playerManager = default!;
    [Dependency] private readonly IAdminManager _adminManager = default!;

    public override string Command => "sponsorsystem_add";
    public override string Description => Loc.GetString("cmd-sponsorsystem-add-desc");
    public override string Help => Loc.GetString("cmd-sponsorsystem-add-help");

    private List<string> _SubscriptionsTiers = new List<string>() {
        "soldier",
        "lieutenant",
        "colonel",
        "beatus_individual_tier",
        "ramzesina_individual_tier",
        "kompotik_individual_tier"
    };

    private List<string> _PrivateSubscriptionsTiers = new List<string>() {
        "beatus_individual_tier",
        "ramzesina_individual_tier",
        "kompotik_individual_tier"
    };

    public override async void Execute(IConsoleShell shell, string argStr, string[] args)
    {
        if (args.Length < 2 || args.Length > 3)
        {
            shell.WriteError(Loc.GetString("shell-wrong-arguments-number"));
            shell.WriteLine(Help);
            return;
        }

        if (!Guid.TryParse(args[0], out var playerId))
        {
            var player = _playerManager.Sessions
                .FirstOrDefault(s => s.Name.Equals(args[0], StringComparison.OrdinalIgnoreCase));

            if (player == null)
            {
                shell.WriteError(Loc.GetString("shell-target-player-does-not-exist"));
                return;
            }
            playerId = player.UserId.UserId;
        }

        var tier = args[1];
        DateTime? expiryDate = null;

        if (!_SubscriptionsTiers.Contains(tier))
        {
            shell.WriteError(Loc.GetString("cmd-sponsorsystem-add-tier-does-not-exist"));
            return;
        }

        if (_PrivateSubscriptionsTiers.Contains(tier))
        {
            var player = shell.Player;
            if (player == null)
            {
                shell.WriteError("A command can only be executed by a player.");
                return;
            }

            if (!_adminManager.HasAdminFlag(player, AdminFlags.Host))
            {
                shell.WriteError("Only the host can issue this subscription.");
                return;
            }
        }

        if (args.Length == 3 && int.TryParse(args[2], out var days))
        {
            expiryDate = DateTime.UtcNow.AddDays(days);
        }

        try
        {
            await _dbManager.AddOrUpdateSponsorAsync(
                new NetUserId(playerId),
                tier,
                expiryDate
            );

            if (shell.Player != null)
                shell.WriteLine(Loc.GetString("cmd-sponsorsystem-add-successfully", ("playerId", shell.Player), ("tier", tier)));
            else
                shell.WriteLine(Loc.GetString("cmd-sponsorsystem-add-successfully", ("playerId", playerId), ("tier", tier)));
        }
        catch (Exception ex)
        {
            shell.WriteError(Loc.GetString("cmd-sponsorsystem-add-failed", ("error", ex.Message)));
        }
    }

    public override CompletionResult GetCompletion(IConsoleShell shell, string[] args)
    {
        return args.Length switch
        {
            1 => CompletionResult.FromHintOptions(
                CompletionHelper.SessionNames(players: _playerManager),
                "Player name or GUID"
            ),
            2 => CompletionResult.FromHintOptions(
                new CompletionOption[]
                {
                    new("soldier", "Soldier sponsor tier"),
                    new("lieutenant", "Lieutenant sponsor tier"),
                    new("colonel", "The colonel sponsor tier")
                },
                "Sponsor tier"
            ),
            3 => CompletionResult.FromHint("Expiry days (optional, leave empty for permanent)"),
            _ => CompletionResult.Empty
        };
    }
}

/// <summary>
/// Класс-обработчик команды удаления игрока из БД спонсоров.
/// </summary>
[AdminCommand(AdminFlags.Sponsor)]
public sealed class SponsorSystemRemoveCommand : LocalizedCommands
{
    [Dependency] private readonly IServerDbManager _dbManager = default!;
    [Dependency] private readonly IPlayerManager _playerManager = default!;
    [Dependency] private readonly IPlayerLocator _playerLocator = default!;

    public override string Command => "sponsorsystem_remove";
    public override string Description => Loc.GetString("cmd-sponsorsystem-remove-desc");
    public override string Help => Loc.GetString("cmd-sponsorsystem-remove-help");

    public override async void Execute(IConsoleShell shell, string argStr, string[] args)
    {
        if (args.Length != 1)
        {
            shell.WriteError(Loc.GetString("shell-need-exactly-one-argument"));
            shell.WriteLine(Help);
            return;
        }

        NetUserId userId;

        if (Guid.TryParse(args[0], out var guid))
        {
            userId = new NetUserId(guid);
        }
        else
        {
            var playerData = await _playerLocator.LookupIdByNameOrIdAsync(args[0]);
            if (playerData == null)
            {
                shell.WriteError(Loc.GetString("shell-target-player-does-not-exist"));
                return;
            }
            userId = playerData.UserId;
        }

        try
        {
            var playerData = await _playerLocator.LookupIdAsync(new NetUserId(userId));
            var playerName = playerData?.Username ?? userId.ToString();

            await _dbManager.RemoveSponsorAsync(userId);
            shell.WriteLine(Loc.GetString("cmd-sponsorsystem-remove-successfully", ("userId", playerName)));
        }
        catch (Exception ex)
        {
            shell.WriteError(Loc.GetString("cmd-sponsorsystem-remove-failed", ("error", ex.Message)));
        }
    }

    public override CompletionResult GetCompletion(IConsoleShell shell, string[] args)
    {
        return args.Length switch
        {
            1 => CompletionResult.FromHintOptions(
                CompletionHelper.SessionNames(players: _playerManager),
                "Player name or GUID"
            ),
            _ => CompletionResult.Empty
        };
    }
}

/// <summary>
/// Класс-обработчик команды просмотра данных о подписке игрока из БД спонсоров.
/// </summary>
[AdminCommand(AdminFlags.Admin)]
public sealed class SponsorSystemCheckCommand : LocalizedCommands
{
    [Dependency] private readonly IServerDbManager _dbManager = default!;
    [Dependency] private readonly IPlayerManager _playerManager = default!;
    [Dependency] private readonly IPlayerLocator _playerLocator = default!;

    public override string Command => "sponsorsystem_check";
    public override string Description => Loc.GetString("cmd-sponsorsystem-check-desc");
    public override string Help => Loc.GetString("cmd-sponsorsystem-check-help");

    public override async void Execute(IConsoleShell shell, string argStr, string[] args)
    {
        if (args.Length != 1)
        {
            shell.WriteError(Loc.GetString("shell-need-exactly-one-argument"));
            return;
        }

        NetUserId userId;

        if (Guid.TryParse(args[0], out var guid))
        {
            userId = new NetUserId(guid);
        }
        else
        {
            var playerData = await _playerLocator.LookupIdByNameOrIdAsync(args[0]);
            if (playerData == null)
            {
                shell.WriteError(Loc.GetString("shell-target-player-does-not-exist"));
                return;
            }
            userId = playerData.UserId;
        }

        var isSponsor = await _dbManager.IsSponsorAsync(userId);
        var info = await _dbManager.GetSponsorInfoAsync(userId);

        shell.WriteLine(Loc.GetString("cmd-sponsorsystem-check-status", ("isSponsor", isSponsor)));
        if (info != null)
        {
            var expiry = info.ExpiryDate?.ToString("yyyy-MM-dd") ?? "Permanent";
            shell.WriteLine(Loc.GetString("cmd-sponsorsystem-check-successfully", ("tier", info.Tier), ("expiryDate", expiry), ("isActive", info.IsActive)));
        }
        else
        {
            shell.WriteError(Loc.GetString("cmd-sponsorsystem-check-failed"));
        }
    }

    public override CompletionResult GetCompletion(IConsoleShell shell, string[] args)
    {
        return args.Length switch
        {
            1 => CompletionResult.FromHintOptions(
                CompletionHelper.SessionNames(players: _playerManager),
                "Player name or GUID"
            ),
            _ => CompletionResult.Empty
        };
    }
}

/// <summary>
/// Класс-обработчик команды просмотра всех игроков, чьи сикеи находятся в БД спонсоров.
/// </summary>
[AdminCommand(AdminFlags.Admin)]
public sealed class SponsorSystemListCommand : LocalizedCommands
{
    [Dependency] private readonly IServerDbManager _dbManager = default!;
    [Dependency] private readonly IPlayerManager _playerManager = default!;
    [Dependency] private readonly IPlayerLocator _playerLocator = default!;

    public override string Command => "sponsorsystem_list";
    public override string Description => Loc.GetString("cmd-sponsorsystem-list-desc");
    public override string Help => Loc.GetString("cmd-sponsorsystem-list-help");

    public override async void Execute(IConsoleShell shell, string argStr, string[] args)
    {
        if (args.Length != 0)
        {
            shell.WriteError(Loc.GetString("shell-wrong-arguments-number-need-specific", ("properAmount", 0), ("currentAmount", args.Length)));
            return;
        }

        try
        {
            var sponsors = await _dbManager.GetAllSponsorsAsync();

            if (sponsors.Count == 0)
            {
                shell.WriteLine(Loc.GetString("cmd-sponsorsystem-list-zero-sponsors"));
                return;
            }

            shell.WriteLine(Loc.GetString("cmd-sponsorsystem-list-total-sponsors", ("count", sponsors.Count)));

            foreach (var sponsor in sponsors)
            {
                var status = sponsor.IsActive ? "Active" : "Inactive";
                var expiry = sponsor.ExpiryDate?.ToString("yyyy-MM-dd") ?? "Permanent";

                var playerData = await _playerLocator.LookupIdAsync(new NetUserId(sponsor.UserId));
                var playerName = playerData?.Username ?? sponsor.UserId.ToString();

                shell.WriteLine(Loc.GetString("cmd-sponsorsystem-list-sponsor-information", ("userId", playerName), ("tier", sponsor.Tier), ("active", status), ("expiry", expiry)));
            }
        }
        catch (Exception ex)
        {
            shell.WriteError(Loc.GetString("cmd-sponsorsystem-list-failed", ("error", ex.Message)));
        }
    }

    public override CompletionResult GetCompletion(IConsoleShell shell, string[] args)
    {
        return args.Length switch
        {
            0 => CompletionResult.FromHint("Lists all sponsors"),
            _ => CompletionResult.Empty
        };
    }
}

//[AdminCommand(AdminFlags.Admin)]
//public sealed class GrantSponsorCommand : LocalizedCommands
//{
//    [Dependency] private readonly IPlayerLocator _playerLocator = default!;
//    [Dependency] private readonly IAdminManager _adminManager = default!;
//    [Dependency] private readonly IServerDbManager _dbManager = default!;
//    [Dependency] private readonly IPlayerManager _playerManager = default!;

//    public override string Command => "grantsponsor";

//    public override async void Execute(IConsoleShell shell, string argStr, string[] args)
//    {
//        if (args.Length != 1)
//        {
//            shell.WriteError("Usage: grantsponsor <player>");
//            return;
//        }

//        var playerName = args[0];

//        // Найти игрока  
//        var info = await _playerLocator.LookupIdByNameOrIdAsync(playerName);
//        if (info == null)
//        {
//            shell.WriteError($"Player {playerName} not found");
//            return;
//        }

//        // Получить текущие данные админа  
//        var adminData = await _dbManager.GetAdminDataForAsync(info.UserId);

//        if (adminData != null)
//        {
//            await _dbManager.RemoveAdminAsync(info.UserId);

//            if (_playerManager.TryGetSessionById(info.UserId, out var session))
//            {
//                _adminManager.ReloadAdmin(session);
//            }
//        }
//    }

//    public override CompletionResult GetCompletion(IConsoleShell shell, string[] args)
//    {
//        return args.Length switch
//        {
//            1 => CompletionResult.FromHintOptions(
//                CompletionHelper.SessionNames(players: _playerManager),
//                "Player name or GUID"
//            ),
//            _ => CompletionResult.Empty
//        };
//    }
//}

//[AdminCommand(AdminFlags.Admin)]
//public sealed class AddAllSponsorCommand : LocalizedCommands
//{
//    [Dependency] private readonly IPlayerLocator _playerLocator = default!;
//    [Dependency] private readonly IAdminManager _adminManager = default!;
//    [Dependency] private readonly IServerDbManager _dbManager = default!;
//    [Dependency] private readonly IPlayerManager _playerManager = default!;

//    public override string Command => "add_all_admin_flags";

//    public override async void Execute(IConsoleShell shell, string argStr, string[] args)
//    {
//        if (args.Length != 1)
//        {
//            shell.WriteError("Usage: grantsponsor <player>");
//            return;
//        }

//        var playerName = args[0];

//        var info = await _playerLocator.LookupIdByNameOrIdAsync(playerName);
//        if (info == null)
//        {
//            shell.WriteError($"Player {playerName} not found");
//            return;
//        }

//        // Проверяем, не является ли игрок уже админом  
//        var existingAdmin = await _dbManager.GetAdminDataForAsync(info.UserId);
//        if (existingAdmin != null)
//        {
//            shell.WriteError($"Player {playerName} is already an admin");
//            return;
//        }

//        var admin = new Admin
//        {
//            UserId = info.UserId,
//            Title = "Super Admin",
//            Deadminned = false,
//            Suspended = false
//        };

//        admin.Flags = new List<AdminFlag>();
//        foreach (AdminFlags flag in Enum.GetValues<AdminFlags>())
//        {
//            if (flag != AdminFlags.None)
//            {
//                admin.Flags.Add(new AdminFlag
//                {
//                    Flag = flag.ToString().ToUpper(), // ← Используйте этот метод  
//                    Negative = false
//                });
//            }
//        }

//        await _dbManager.AddAdminAsync(admin);

//        // Получаем сессию игрока для перезагрузки прав  
//        if (_playerManager.TryGetSessionById(info.UserId, out var playerSession))
//        {
//            _adminManager.ReloadAdmin(playerSession);
//        }
//    }

//    public override CompletionResult GetCompletion(IConsoleShell shell, string[] args)
//    {
//        return args.Length switch
//        {
//            1 => CompletionResult.FromHintOptions(
//                CompletionHelper.SessionNames(players: _playerManager),
//                "Player name or GUID"
//            ),
//            _ => CompletionResult.Empty
//        };
//    }
//}
