using SQLite;
using MyAndroidApp.Models;

namespace MyAndroidApp.Services;

public class MemberDatabase
{
    private SQLiteAsyncConnection _database;

    // リセットされたことを他の画面に通知するためのイベント
    public event Action OnEventReset;

    public MemberDatabase()
    {
    }

    // データベースの初期化
    private async Task Init()
    {
        if (_database is not null)
            return;

        // スマホ内部の安全なデータ保存領域に「members.db3」というファイルを作成
        string dbPath = Path.Combine(FileSystem.AppDataDirectory, "members.db3");
        _database = new SQLiteAsyncConnection(dbPath);
        
        // テーブルを作成（すでに存在する場合は何もしない）
        await _database.CreateTableAsync<Member>();
    }

    // 全メンバーの取得
    public async Task<List<Member>> GetMembersAsync()
    {
        await Init();
        return await _database.Table<Member>().ToListAsync();
    }

    // メンバーの保存（新規登録 または 更新）
    public async Task<int> SaveMemberAsync(Member item)
    {
        await Init();
        // IDで検索し、すでに存在するか確認
        var existingMember = await _database.Table<Member>().Where(m => m.Id == item.Id).FirstOrDefaultAsync();
        
        if (existingMember != null)
        {
            // 更新
            return await _database.UpdateAsync(item);
        }
        else
        {
            // 新規登録
            return await _database.InsertAsync(item);
        }
    }

    // メンバーの削除
    public async Task<int> DeleteMemberAsync(Member item)
    {
        await Init();
        return await _database.DeleteAsync(item);
    }

    // 全メンバーの参加状態・試合回数・休憩回数をリセット、およびビジターの削除
    public async Task ResetAllParticipationAsync()
    {
        await Init();
        var members = await GetMembersAsync();
        foreach (var member in members)
        {
            if (member.Type == MemberType.ビジター)
            {
                // ビジターは削除
                await _database.DeleteAsync(member);
            }
            else
            {
                // メンバーは状態リセットのみ
                member.IsParticipating = false;
                member.MatchCount = 0;
                member.BreakCount = 0;
                await _database.UpdateAsync(member);
            }
        }

        // リセット完了を通知する
        OnEventReset?.Invoke();
    }
}
