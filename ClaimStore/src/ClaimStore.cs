using System;
using System.Collections.Generic;
using Vintagestory.API.Common;
using Vintagestory.API.Config;
using Vintagestory.API.Server;

namespace ClaimStore.src
{
    public class ClaimStoreConfig
    {
        public int BlocksPerUnit { get; set; } = 5000;
        public int PricePerUnit { get; set; } = 3;
        public int PriceByArea { get; set; } = 50; 
    }

    public class ClaimStore : ModSystem
    {
        private int BlocksPerUnit = 5000;
        private int PricePerUnit = 3;
        private int PriceByArea = 20;
        private ICoreServerAPI serverApi;

        public override void StartServerSide(ICoreServerAPI api)
        {
            base.StartServerSide(api);
            serverApi = api;

            // Lê ou cria config
            var config = api.LoadModConfig<ClaimStoreConfig>("config.json") ?? new ClaimStoreConfig();
            BlocksPerUnit = config.BlocksPerUnit;
            PricePerUnit = config.PricePerUnit;
            PriceByArea = config.PriceByArea;
            api.StoreModConfig(config, "config.json");

            api.ChatCommands
                .Create("claimstore")
                .WithDescription("💰 ClaimStore Mod Commands 💰")
                .RequiresPlayer()
                .RequiresPrivilege("areamodify")

                // ====== SUBCOMANDO CLAIM ======
                .BeginSubCommand("claim")
                    .BeginSubCommand("buy")
                        .WithDescription("Compra blocos de claim com engrenagens.")
                        .WithArgs(api.ChatCommands.Parsers.Int("quantidade"))
                        .HandleWith(OnBuyClaimCommand)
                    .EndSubCommand()
                    .BeginSubCommand("set")
                        .WithDescription("Configura preço/blocos por unidade (admin).")
                        .RequiresPrivilege("controlserver")
                        .WithArgs(api.ChatCommands.Parsers.Int("blocksPerUnit"), api.ChatCommands.Parsers.Int("pricePerUnit"))
                        .HandleWith(OnConfigClaimCommand)
                    .EndSubCommand()
                    .BeginSubCommand("stats")
                        .WithDescription("Mostra as configurações de claim.")
                        .HandleWith(OnStatsClaimCommand)
                    .EndSubCommand()
                .EndSubCommand()

                // ====== SUBCOMANDO AREA ======
                .BeginSubCommand("area")
                    .BeginSubCommand("buy")
                        .WithDescription("Compra áreas de claim adicionais.")
                        .WithArgs(api.ChatCommands.Parsers.Int("quantidade"))
                        .HandleWith(OnBuyAreaCommand)
                    .EndSubCommand()
                    .BeginSubCommand("set")
                        .WithDescription("Configura preço por área (admin).")
                        .RequiresPrivilege("controlserver")
                        .WithArgs(api.ChatCommands.Parsers.Int("priceByArea"))
                        .HandleWith(OnConfigAreaCommand)
                    .EndSubCommand()
                    .BeginSubCommand("stats")
                        .WithDescription("Mostra o preço por área.")
                        .HandleWith(OnStatsAreaCommand)
                    .EndSubCommand()
                .EndSubCommand();
        }

        // ==== CLAIM ====
        private TextCommandResult OnStatsClaimCommand(TextCommandCallingArgs args)
        {
            return TextCommandResult.Success(
                $"Blocos por unidade: {BlocksPerUnit}, Preço por unidade: {PricePerUnit} engrenagens."
            );
        }

        private TextCommandResult OnConfigClaimCommand(TextCommandCallingArgs args)
        {
            BlocksPerUnit = (int)args[0];
            PricePerUnit = (int)args[1];
            serverApi.StoreModConfig(new ClaimStoreConfig { BlocksPerUnit = BlocksPerUnit, PricePerUnit = PricePerUnit, PriceByArea = PriceByArea }, "config.json");
            return TextCommandResult.Success($"Configuração de claim atualizada: {BlocksPerUnit} blocos por {PricePerUnit} engrenagens.");
        }

        private TextCommandResult OnBuyClaimCommand(TextCommandCallingArgs args)
        {
            IServerPlayer player = args.Caller.Player as IServerPlayer;
            int quantidade = (int)args[0];
            int engrenagensNecessarias = (int)Math.Ceiling((quantidade / (double)BlocksPerUnit) * PricePerUnit);

            if (!TrySpendGears(player, engrenagensNecessarias))
                return TextCommandResult.Error($"Você precisa de {engrenagensNecessarias} engrenagens.");

            int oldValue = player.ServerData.ExtraLandClaimAllowance;
            player.ServerData.ExtraLandClaimAllowance += quantidade;
            PlayPurchaseSound(player);

            player.SendMessage(GlobalConstants.GeneralChatGroup,
                $"Você comprou {quantidade} blocos de claim. Antes: {oldValue} → Agora: {player.ServerData.ExtraLandClaimAllowance}",
                EnumChatType.CommandSuccess);

            return TextCommandResult.Success("Compra realizada.");
        }

        // ==== AREA ====
        private TextCommandResult OnStatsAreaCommand(TextCommandCallingArgs args)
        {
            return TextCommandResult.Success($"Preço por área: {PriceByArea} engrenagens.");
        }

        private TextCommandResult OnConfigAreaCommand(TextCommandCallingArgs args)
        {
            PriceByArea = (int)args[0];
            serverApi.StoreModConfig(new ClaimStoreConfig { BlocksPerUnit = BlocksPerUnit, PricePerUnit = PricePerUnit, PriceByArea = PriceByArea }, "config.json");
            return TextCommandResult.Success($"Preço por área atualizado para {PriceByArea} engrenagens.");
        }

        private TextCommandResult OnBuyAreaCommand(TextCommandCallingArgs args)
        {
            IServerPlayer player = args.Caller.Player as IServerPlayer;
            int quantidade = (int)args[0];
            int engrenagensNecessarias = quantidade * PriceByArea;

            if (!TrySpendGears(player, engrenagensNecessarias))
                return TextCommandResult.Error($"Você precisa de {engrenagensNecessarias} engrenagens.");

            int oldValue = player.ServerData.ExtraLandClaimAreas;
            player.ServerData.ExtraLandClaimAreas += quantidade;
            PlayPurchaseSound(player);

            player.SendMessage(GlobalConstants.GeneralChatGroup,
                $"Você comprou {quantidade} áreas de claim. Antes: {oldValue} → Agora: {player.ServerData.ExtraLandClaimAreas}",
                EnumChatType.CommandSuccess);

            return TextCommandResult.Success("Compra de área realizada.");
        }

        // ==== AUXILIARES ====
        private bool TrySpendGears(IServerPlayer player, int amount)
        {
            int gears = CountGears(player);
            if (gears < amount) return false;
            return RemoveGears(player, amount);
        }

        private int CountGears(IServerPlayer player)
        {
            var inventories = new List<IInventory> {
                player.InventoryManager.GetOwnInventory(GlobalConstants.characterInvClassName),
                player.InventoryManager.GetOwnInventory(GlobalConstants.hotBarInvClassName),
                player.InventoryManager.GetOwnInventory(GlobalConstants.backpackInvClassName)
            };
            int total = 0;
            foreach (var inv in inventories) total += CountGearsInInventory(inv);
            return total;
        }

        private int CountGearsInInventory(IInventory inv)
        {
            int count = 0;
            for (int i = 0; i < inv.Count; i++)
            {
                var slot = inv[i];
                if (slot?.Itemstack != null && slot.Itemstack.Collectible.Code.Equals(new AssetLocation("gear-rusty")))
                    count += slot.Itemstack.StackSize;
            }
            return count;
        }

        private bool RemoveGears(IServerPlayer player, int amount)
        {
            var inventories = new List<IInventory>
            {
                player.InventoryManager.GetOwnInventory(GlobalConstants.characterInvClassName),
                player.InventoryManager.GetOwnInventory(GlobalConstants.hotBarInvClassName),
                player.InventoryManager.GetOwnInventory(GlobalConstants.backpackInvClassName)
            };
            int remaining = amount;
            foreach (var inv in inventories)
            {
                if (inv == null) continue;
                for (int i = 0; i < inv.Count && remaining > 0; i++)
                {
                    var slot = inv[i];
                    if (slot?.Itemstack != null && slot.Itemstack.Collectible.Code.Equals(new AssetLocation("gear-rusty")))
                    {
                        int toRemove = Math.Min(slot.Itemstack.StackSize, remaining);
                        slot.Itemstack.StackSize -= toRemove;
                        remaining -= toRemove;
                        if (slot.Itemstack.StackSize <= 0) slot.Itemstack = null;
                        inv.MarkSlotDirty(i);
                    }
                }
            }
            return remaining <= 0;
        }

        private void PlayPurchaseSound(IServerPlayer player)
        {
            player.Entity.World.PlaySoundAt(new AssetLocation("sounds/effect/cashregister"), player.Entity, null, false, 16f);
        }
    }
}
