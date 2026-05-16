namespace VocabApp.Core.Csv;

/// <summary>
/// インポート時、同じ単語 (word + part_of_speech) が既に存在する場合の挙動。
/// </summary>
public enum ConflictMode
{
    /// <summary>既存を残し、新規行をスキップする。</summary>
    Skip,

    /// <summary>既存の単語の内容を CSV の値で上書きする。CSV に学習統計列が
    /// 含まれない場合は、その項目は更新しない。</summary>
    Overwrite,

    /// <summary>衝突を無視して別レコードとして追加する。</summary>
    AddAsNew,
}
