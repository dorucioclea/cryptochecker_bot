using SQLite;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace MetaTools.Database
{
    
    public class Subscription
    {
        [PrimaryKey, AutoIncrement]
        public long id { get; set; }
        public long tg_id { get; set; }
        public string name { get; set; }
        public DateTime ends { get; set; }

        public static async Task<List<Subscription>> GetActiveSubs(SQLiteAsyncConnection db, long user_id)
        {
            List<Subscription> active_subscriptions = new List<Subscription>();
            
            foreach (Subscription sub in (await db.Table<Subscription>().ToListAsync()).Where(s => s.tg_id == user_id))
            {
                if (sub.ends > DateTime.Now)
                {
                    active_subscriptions.Add(sub);
                }
            }
            return active_subscriptions;
        }
        public static async Task set_Subscription(SQLiteAsyncConnection db, long user_id, string name, int days)
        {
            var sub = (await db.Table<Subscription>().ToListAsync()).Where(s => s.tg_id == user_id && s.name == name);
            if (sub.Count() == 0)
            {
                await db.InsertAsync(new Subscription()
                {
                    tg_id = user_id,
                    name = name,
                    ends = DateTime.Now.AddDays(days)
                });
            }
            else
            {
                var sub_first = sub.First();
                sub_first.ends = DateTime.Now.AddDays(days);
                await db.UpdateAsync(sub_first);
            }
        }
    }

}
