using System;
using System.Collections.Generic;
using Vintagestory.API.Common;
using Vintagestory.API.Config;
using Vintagestory.API.Server;

namespace ClaimStore.src
{
    public class ClaimStore : ModSystem
    {
        private int BlocksPerUnit = 5000;
        private int PricePerUnit = 3;
        private ICoreServerAPI serverApi;



        public override void StartServerSide(ICoreServerAPI api)
        {
            base.StartServerSide(api);

            serverApi = api;

            // Lê o config.json
            var config = api.LoadModConfig<ClaimStoreConfig>("config.json");
            if (config == null)
            {
                config = new ClaimStoreConfig();
                api.StoreModConfig(config, "config.json");
            }

            BlocksPerUnit = config.BlocksPerUnit;
            PricePerUnit = config.PricePerUnit;

            //CHAT COMMANDS

            api.ChatCommands
                 .Create("claimstore")
                 .WithDescription("💰 ClaimStore Mod Commands 💰")
                 .RequiresPlayer()
                 .RequiresPrivilege("areamodify")
                 .BeginSubCommand("buy")
                     .WithDescription("Permite a compra de claim usando engrenagens enferrujadas")
                     .RequiresPrivilege("areamodify")
                     .RequiresPlayer()
                     .WithArgs(api.ChatCommands.Parsers.Int("quantidade"))
                     .HandleWith(OnBuyClaimCommand)
                 .EndSubCommand()
                 .BeginSubCommand("set")
                     .WithDescription("Configura preço e blocos por unidade (admin)")
                     .RequiresPrivilege("controlserver")
                     .WithArgs(api.ChatCommands.Parsers.Int("blocksPerUnit"), api.ChatCommands.Parsers.Int("pricePerUnit"))
                     .HandleWith(OnConfigCommand)
                 .EndSubCommand()
                 .BeginSubCommand("stats")
                     .WithDescription("Mostra as configurações atuais")
                     .RequiresPrivilege("areamodify")
                     .HandleWith(OnConfigStatsCommand)
                 .EndSubCommand();
        }


        private TextCommandResult OnConfigStatsCommand(TextCommandCallingArgs args)
        {
            return TextCommandResult.Success(
                $"Blocos por unidade: {BlocksPerUnit}, Preço por unidade: {PricePerUnit} engrenagens."
            );
        }

        private TextCommandResult OnConfigCommand(TextCommandCallingArgs args)
        {
            BlocksPerUnit = (int)args[0];
            PricePerUnit = (int)args[1];

            // Salva no arquivo
            serverApi.StoreModConfig(new ClaimStoreConfig { BlocksPerUnit = BlocksPerUnit, PricePerUnit = PricePerUnit }, "config.json");

            return TextCommandResult.Success($"Configuração atualizada: {BlocksPerUnit} blocos por {PricePerUnit} engrenagens.");
        }

        private int CountGearsInInventory(IInventory inv)
        {
            int count = 0;
            for (int i = 0; i < inv.Count; i++)
            {
                var slot = inv[i];
                if (slot?.Itemstack != null && slot.Itemstack.Collectible.Code.Equals(new AssetLocation("gear-rusty")))
                {
                    count += slot.Itemstack.StackSize;
                }
            }
            return count; //
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
                        if (slot.Itemstack.StackSize <= 0)
                        {
                            slot.Itemstack = null;
                        }
                        inv.MarkSlotDirty(i);
                    }
                }
            }
            return remaining <= 0;
        }

        private TextCommandResult OnBuyClaimCommand(TextCommandCallingArgs args)
        {
            IServerPlayer player = args.Caller.Player as IServerPlayer;

            int quantidade = (int)args[0];
            int engrenagensNecessarias = (int)Math.Ceiling((quantidade / (double)BlocksPerUnit) * PricePerUnit);
            int engrenagensNoInventario = CountGears(player);
            if (engrenagensNoInventario < engrenagensNecessarias)
            {
                player.SendMessage(GlobalConstants.GeneralChatGroup, $"Você precisa de {engrenagensNecessarias} engrenagens, mas só tem {engrenagensNoInventario}.", EnumChatType.CommandError);
                return TextCommandResult.Error("Engrenagens insuficientes.");
            }
            if (!RemoveGears(player, engrenagensNecessarias))
            {
                player.SendMessage(GlobalConstants.GeneralChatGroup, "Erro ao remover as engrenagens do inventário.", EnumChatType.CommandError);
                return TextCommandResult.Error("Falha na remoção das engrenagens.");
            }



            int oldExtraLandClaim = player.ServerData.ExtraLandClaimAllowance;
            player.ServerData.ExtraLandClaimAllowance += quantidade;

            player.Entity.World.PlaySoundAt(
                    new AssetLocation("sounds/effect/cashregister"),
                    player.Entity, null, false, 16f
                );

            player.SendMessage(GlobalConstants.GeneralChatGroup,
                $"Você comprou {quantidade} blocos de claim por {engrenagensNecessarias} engrenagens! " +
                $"Você possuía {oldExtraLandClaim}m³, agora possui {player.ServerData.ExtraLandClaimAllowance}m³",
                EnumChatType.CommandSuccess);

            return TextCommandResult.Success("Compra realizada.");

        }


        private int CountGears(IServerPlayer player)
        {
            var inventories = new List<IInventory> {
                player.InventoryManager.GetOwnInventory(GlobalConstants.characterInvClassName),
                player.InventoryManager.GetOwnInventory(GlobalConstants.hotBarInvClassName),
                player.InventoryManager.GetOwnInventory(GlobalConstants.backpackInvClassName)
            };
            int totalGears = 0;
            foreach (var inv in inventories)
            {
                if (inv == null) continue;
                totalGears += CountGearsInInventory(inv);
            }
            return totalGears;
        }

    }
}

