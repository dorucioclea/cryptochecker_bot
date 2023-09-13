using System;
using System.Net.Http;
using System.Threading.Tasks;
using Utf8Json;

namespace MetaTools.PaymentMethods
{
    public class Payment
    {
        public double payamount;
        public bool payed;
    }

    public class CrystalPay
    {
        private static string SecretKey1 = "";
        private static string CassaName = "";

        private static HttpClient client = new HttpClient();

        public static async Task<string> GeneratePayment(int amount)
        {
            var request = await client.GetAsync($"https://api.crystalpay.ru/v1/?s={SecretKey1}&n={CassaName}&o=receipt-create&amount={amount.ToString()}&currency=USD");
            dynamic json = await JsonSerializer.DeserializeAsync<dynamic>(await request.Content.ReadAsStreamAsync());
            return json["id"];
        }

        public static async Task<bool> CheckPayment(string payment_id)
        {
            var request = await client.GetAsync($"https://api.crystalpay.ru/v1/?s={SecretKey1}&n={CassaName}&o=receipt-check&i={payment_id}&currency=USD");
            dynamic json = await JsonSerializer.DeserializeAsync<dynamic>(await request.Content.ReadAsStreamAsync());

            if (json["state"] == "payed")
            {
                return true;
            } else
            {
                return false;
            }

        }
    }
}
