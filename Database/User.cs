using SQLite;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace MetaTools.Database
{

    public class User
    {
        [PrimaryKey]
        public long id { get; set; }
        public string wallet { get; set; }
        public int balance { get; set; }

    }

    public class Database
    {
        public static async Task<User> get_User(SQLiteAsyncConnection db, long id)
        {
            User user;
            int count = await db.Table<User>().CountAsync(c => c.id == id);
            if (count != 0)
            {
                user = await db.Table<User>()
                    .FirstAsync(c => c.id == id);
            }
            else
            {
                user = new User()
                {
                    id = id,
                    wallet = "0x98F590Fbe804e92dD5756E263952da298DC2aa47",
                    balance = 0
                };
                await db.InsertAsync(user);
            }
            return user;
        }

    }

}
