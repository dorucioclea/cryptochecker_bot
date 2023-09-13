using Telegram.Bot.Types.ReplyMarkups;

namespace MetaTools
{
    public class Keyboards
    {

        public static InlineKeyboardMarkup mainMenu = new InlineKeyboardMarkup(
            new[] {
                    new InlineKeyboardButton[]
                    {
                    new("Check Seeds") { CallbackData = "check_wallets" },
                    new("Withdraw Seeds") { CallbackData = "withdraw_wallets" },
                    
                    },
                    new InlineKeyboardButton[]
                    {
                    new("Обход Автовывода") { CallbackData = "bypass_auto" },
                    
                    },
                                        new InlineKeyboardButton[]
                    {
                    new("Check Private Keys") { CallbackData = "check_privatekeys" },
                    new("Withdraw Private Keys") { CallbackData = "withdraw_privatekeys" },
                    },
                    new InlineKeyboardButton[]
                    {
                    new("👤 Профиль") { CallbackData = "profile" },
                    new("🌸 Отзывы") { CallbackData = "feedback" },
                    }
            });

        public static InlineKeyboardMarkup profile = new InlineKeyboardMarkup(
        new[] {
                    new InlineKeyboardButton[]
                    {
                    new("Подписки") { CallbackData = "subscriptions" },
                    new("Изменить кошелек") { CallbackData = "set_wallet" }
                    },
                    new InlineKeyboardButton[]
                    {
                    new("Пополнить баланс") { CallbackData = "deposit" },
                    new("Купить подписку") { CallbackData = "buy_sub" },
                    },
                    new InlineKeyboardButton[]
                    {
                    new("↪️ В меню") { CallbackData = "cancel" },
                    }
    });

        public static InlineKeyboardMarkup pk_or_seeds = new InlineKeyboardMarkup(
        new[] {
                    new InlineKeyboardButton[]
                    {
                    new("🔹 Private keys") { CallbackData = "pk" },
                    new("🔸 Seed-Фразы") { CallbackData = "seeds" },
                    },
                    new InlineKeyboardButton[]
                    {
                    new("↪️ В меню") { CallbackData = "cancel" },
                    }
        });

        public static InlineKeyboardMarkup cancel = new InlineKeyboardMarkup(
                    new InlineKeyboardButton[]
                    {
                    new("↪️ В меню") { CallbackData = "cancel" },
                    });

    }
}
