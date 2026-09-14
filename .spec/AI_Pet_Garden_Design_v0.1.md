# AI Pet Garden（仮称） 基本設計書 / v0.1

**文書種別:** 実装用基本設計書  
**対象:** Windows デスクトップアプリ  
**想定技術:** .NET 10 / WPF / C#  
**作成日:** 2026-09-14  
**ステータス:** v0.1 実装開始可能

## 1. 目的

Codex のカスタムペット資産を読み込み、複数のペットを Windows デスクトップ上に同時表示する。各ペットは独立した AI キャラクターとしてチャット可能とし、一定確率で「今の回答」を、起きている別ペットへ Relay する。

本アプリの主眼は「複数AIチャット画面」ではなく、**デスクトップ上で複数のAIキャラクターが生活しているように見える対話UI**を実現することにある。

## 2. v0.1 のスコープ

### 実装する
- `%USERPROFILE%\.codex\pets` の読取専用スキャン
- 複数ペットの同時表示
- ペットアニメーション再生
- ドラッグ移動、位置保存
- ペットの起床/睡眠/非表示
- ペットクリックで専用チャットウィンドウ表示
- ペット単位の Persona / ProjectRef / Backend / Model 設定
- ペット単位の独立会話履歴
- 回答完了時のランダム Relay
- Relay は最大1匹、Relay 応答から再Relayしない
- 設定、会話、Relay 履歴の永続化
- APIキー等の秘密情報を平文設定へ保存しない

### v0.1 では実装しない
- ChatGPT Project のWeb画面DOM自動操作
- Relay の連鎖
- 1回答から複数ターゲットへの同時Relay
- ペット同士の自律会話ループ
- 音声入出力
- 長期記憶検索/RAG
- プロバイダー横断の自動モデル選択
- ネットワーク越しのマルチPC同期

## 3. 重要な設計方針

1. Codex のペット資産は**読取専用**で扱い、改変しない。
2. ペット表示、Persona、チャット、Relay を疎結合にする。
3. ChatGPT Project は `ProjectRef` としてペットに対応付けるが、会話実装は `ChatBackend` で抽象化する。
4. Project への直接送受信手段の有無に依存しない。v0.1 はプログラムから送受信できる Backend を利用する。
5. Relay は「偶発的な横会話」であり、エージェント連鎖処理にはしない。
6. エラー発生時もペット表示だけは可能な限り継続する。

## 4. ユースケース

- ユーザーはアプリを起動すると、登録済みペットを複数同時に表示できる。
- ペットをクリックすると、そのキャラクター専用のチャットウィンドウが開く。
- 各ペットは独立した Persona と会話履歴を持つ。
- 回答が返ると、設定確率で別の「起きている」ペットにその回答が Relay される。
- Relay を受けたペットは、自分の Persona に従って反応する。
- Relay 応答は履歴へ残るが、そこから別ペットへ再Relayしない。

## 5. 機能要件

| ID | 機能 | 要件 |
|---|---|---|
| FR-001 | Pet Scan | `%USERPROFILE%\.codex\pets` を起動時および手動再読込時に走査する |
| FR-002 | Pet Load | `pet.json` とスプライト画像を読み込む。未知/不完全データは個別エラーとして隔離する |
| FR-003 | Multi Pet | 複数ペットを同時表示できる |
| FR-004 | Position | ペットをドラッグ移動でき、位置を保存/復元する |
| FR-005 | Awake State | Awake / Sleeping / Hidden を切り替えられる |
| FR-006 | Chat Open | Awake または Sleeping のペットをクリックし専用チャットを開ける。Hidden は画面上に存在しない |
| FR-007 | Persona | ペットごとに System Prompt / Greeting / ProjectRef を持てる |
| FR-008 | Backend | ペットごとに Backend / Model を指定できる |
| FR-009 | History | 会話履歴をペット単位で分離して保存する |
| FR-010 | Relay Trigger | Human 起点の通常回答完了後のみ Relay 判定する |
| FR-011 | Relay Target | Awake、可視、Chat有効、非Busy、送信元以外から最大1匹選ぶ |
| FR-012 | No Chain | Relay 起点の応答は Relay 判定対象外とする |
| FR-013 | Relay History | Relay送受信イベントと応答を履歴へ保存する |
| FR-014 | Secrets | APIキー等を平文JSONへ保存しない |
| FR-015 | Recovery | 1匹の読込/チャット失敗が他ペットへ波及しない |
| FR-016 | Reload | ペット資産を手動再読込できる |
| FR-017 | Project Open | ProjectRef にURLがある場合は「Projectを開く」操作を提供できる |

## 6. 状態定義

### PetLifeState
- `Awake`: 画面表示中。Relay受信候補。
- `Sleeping`: 画面表示中だが Relay受信候補外。チャットは手動で開ける。
- `Hidden`: 画面非表示。Relay受信候補外。

### PetActivityState
- `Idle`: 待機中
- `Thinking`: AI応答待ち
- `Talking`: 応答表示中
- `Error`: 直近処理でエラー

LifeState と ActivityState は独立して保持する。

## 7. Relay 仕様

### 7.1 発火条件
- 発話の `Origin == Human` であること。
- AI回答が正常終了していること。
- `RelayEnabled == true`。
- 0.0〜1.0 の乱数が `RelayProbability` 未満であること。
- 候補ペットが1匹以上存在すること。

### 7.2 ターゲット候補
- `PetLifeState == Awake`
- `IsVisible == true`
- `ChatEnabled == true`
- `PetActivityState != Thinking`
- 送信元ペットではない

### 7.3 Relay 制約
- 1回答につき最大1匹。
- Relay応答の `Origin == Relay` とし、再Relay禁止。
- 同一 `SourceMessageId` から二重Relayしない。
- 受信先が処理開始直前に Busy になった場合は Relay を中止し、再抽選しない。

### 7.4 Relay Payload
```json
{
  "relayEventId": "uuid",
  "sourcePetId": "pet-chatgpt",
  "sourceDisplayName": "ChatGPTちゃん",
  "sourceMessageId": "uuid",
  "responseText": "...",
  "origin": "HumanResponse",
  "relayDepth": 1,
  "createdAt": "ISO-8601"
}
```

### 7.5 受信プロンプト
```text
[PET_RELAY]
送信元: {sourceDisplayName}

{sourceDisplayName} がユーザーへの返答として次の内容を話しました。
あなた自身のキャラクターと立場を保ったまま、必要なら自然に反応してください。
反応する内容がなければ短く受け流して構いません。

---
{responseText}
---
```

## 8. ChatGPT Project 対応方針

各 PetProfile に以下を保持する。
- `ProjectRef.Name`
- `ProjectRef.Url`（任意）
- `Persona.SystemPrompt`
- `Backend.Type`
- `Backend.Model`

v0.1 では ProjectRef は**論理対応および手動でProjectを開くための参照情報**として扱う。外部アプリからProjectへ直接送受信できる手段が利用可能になった場合は、`IChatBackend` の実装として追加し、UI/Relay層を変更しない。

## 9. システム構成

```text
%USERPROFILE%\.codex\pets
          |
          v
   +----------------+
   | PetAssetLoader |
   +--------+-------+
            |
            v
   +----------------+       +------------------+
   | PetRepository  |<----->| ProfileRepository|
   +--------+-------+       +------------------+
            |
            v
   +----------------+
   | PetManager     |
   +--+----------+--+
      |          |
      v          v
 +---------+  +-----------+
 |PetWindow|  |ChatWindow |
 +---------+  +-----+-----+
                    |
                    v
             +-------------+
             | ChatService |
             +------+------+ 
                    |
          +---------+----------+
          |                    |
          v                    v
 +----------------+    +----------------+
 | IChatBackend   |    | RelayManager   |
 +----------------+    +-------+--------+
                              |
                              v
                       other Pet ChatService
```

## 10. 主要コンポーネント責務

| コンポーネント | 責務 |
|---|---|
| `PetAssetLoader` | `.codex\pets` の走査、pet metadata/画像読込、互換性吸収 |
| `PetRepository` | 検出済みペット定義の保持 |
| `ProfileRepository` | Persona、ProjectRef、Backend、表示設定の保存/読込 |
| `PetManager` | PetInstance の生成/破棄、LifeState、位置、表示制御 |
| `SpriteRenderer` | WebP等の画像読込、フレーム切出し、アニメーション再生 |
| `PetWindow` | 透明枠なし表示、クリック、ドラッグ、状態アニメーション |
| `ChatWindow` | ペット専用会話UI、履歴表示、入力、送信 |
| `ChatService` | Prompt構築、履歴統合、Backend呼出し、状態遷移 |
| `IChatBackend` | プロバイダー差異の抽象化 |
| `RelayManager` | Relay判定、ターゲット選択、Payload生成、二重送信防止 |
| `HistoryRepository` | 会話/Relayイベントの永続化 |
| `SecretStore` | APIキー等の暗号化保存/取得 |
| `AppSettingsService` | グローバル設定の保存/読込 |

## 11. Chat Backend インターフェース

```csharp
public interface IChatBackend
{
    string BackendId { get; }

    Task<ChatBackendResult> SendAsync(
        ChatRequest request,
        CancellationToken cancellationToken);
}
```

`ChatRequest` は以下を含む。
- PetId
- SystemPrompt
- ConversationMessages
- CurrentInput
- Model
- RequestOrigin (`Human` / `Relay`)
- RelayMetadata（任意）

Backend はUIやRelayロジックを直接参照しない。

## 12. データ設計

### 12.1 PetProfile JSON
```json
{
  "petId": "pet-chatgpt",
  "displayName": "ChatGPTちゃん",
  "petAssetId": "codex-pet-id",
  "projectRef": {
    "name": "ChatGPTちゃん Project",
    "url": ""
  },
  "persona": {
    "systemPrompt": "...",
    "greeting": "今日は何をするのじゃ？"
  },
  "backend": {
    "type": "OpenAICompatible",
    "model": "model-name",
    "secretKeyRef": "secret/pet-chatgpt"
  },
  "relay": {
    "enabled": true
  },
  "view": {
    "lifeState": "Awake",
    "x": 1200,
    "y": 700,
    "scale": 1.0,
    "topMost": true
  }
}
```

### 12.2 AppSettings JSON
```json
{
  "petRoot": "%USERPROFILE%\\.codex\\pets",
  "relayEnabled": true,
  "relayProbability": 0.15,
  "maxRelayTargets": 1,
  "allowRelayChain": false,
  "historyRetentionDays": 0
}
```

`historyRetentionDays == 0` は自動削除なし。

### 12.3 会話DB
SQLite を使用する。主なテーブル:
- `conversations`
- `messages`
- `relay_events`

`messages.origin` は `Human`, `Assistant`, `RelayInput`, `RelayResponse`, `SystemEvent` を保持する。

## 13. ファイル配置

```text
%LOCALAPPDATA%\AI-Pet-Garden\
├─ settings.json
├─ profiles\
│  ├─ pet-chatgpt.json
│  ├─ pet-gemini.json
│  └─ ...
├─ data\
│  └─ history.db
├─ secrets\
│  └─ protected.dat
└─ logs\
   └─ app-yyyyMMdd.log

%USERPROFILE%\.codex\pets\
└─ ...   # 読取専用
```

## 14. UI設計

### 14.1 PetWindow
- 枠なし、背景透明。
- スプライトサイズに追従。
- 左クリック: ChatWindow を開く/前面へ。
- 左ドラッグ: 移動。
- 右クリック: コンテキストメニュー。
  - 話す
  - 起こす / 寝かせる
  - 非表示
  - Projectを開く
  - 設定
- スケール変更可能。
- TopMost は個別設定可能。

### 14.2 ChatWindow
```text
+-------------------------------------+
| ChatGPTちゃん   [Project] [設定]    |
+-------------------------------------+
|                                     |
|  conversation history               |
|                                     |
|  [Relay from Geminiちゃん]          |
|  ...                                |
|                                     |
+-------------------------------------+
| [入力............................]   |
|                            [送信]    |
+-------------------------------------+
```

- ChatWindow はペット単位で1つ。
- 閉じても履歴は保持。
- Relay受信時に閉じている場合、未読数をPetWindowに表示可能。
- v0.1では未読バッジは推奨機能。必須ではない。

### 14.3 SettingsWindow
- 検出ペット一覧
- Awake / Sleeping / Hidden
- Persona編集
- ProjectRef編集
- Backend / Model設定
- Relay ON/OFF
- グローバルRelay確率
- TopMost / Scale
- APIキー登録/更新

## 15. アニメーション設計

Codex pet metadata の差異を吸収するため、`IPetSpriteAdapter` を設ける。

```csharp
public interface IPetSpriteAdapter
{
    bool CanHandle(PetAssetMetadata metadata);
    SpriteLayout ResolveLayout(PetAssetMetadata metadata);
    AnimationMap ResolveAnimations(PetAssetMetadata metadata);
}
```

優先順位:
1. metadata に明示されたアニメーション定義
2. 対応済み sprite version adapter
3. fallback adapter
4. 読込不可として当該ペットのみエラー表示

v0.1で最低限必要な論理アニメーション:
- Idle
- Thinking
- Talking
- Error

物理フレームへの割当は Adapter が決定する。

## 16. チャット処理フロー

```text
User Send
   |
   v
ChatService
   |-- load profile/persona
   |-- load recent history
   |-- set PetActivity=Thinking
   v
IChatBackend.SendAsync
   |
   +-- error --> Error state + UI message
   |
   v
save Assistant message
   |
   v
PetActivity=Talking -> Idle
   |
   v
RelayManager.TryRelayAsync
   |-- miss/no candidate --> END
   |
   v
Target ChatService.ReceiveRelayAsync
   |
   v
save RelayInput
   |
   v
IChatBackend.SendAsync(origin=Relay)
   |
   v
save RelayResponse
   |
   v
END  # 再Relay禁止
```

## 17. Relay 擬似コード

```csharp
async Task TryRelayAsync(PetId sourcePetId, Message sourceResponse)
{
    if (!settings.RelayEnabled) return;
    if (sourceResponse.Origin != MessageOrigin.AssistantFromHuman) return;
    if (await history.HasRelayForSourceAsync(sourceResponse.Id)) return;
    if (random.NextDouble() >= settings.RelayProbability) return;

    var candidates = petManager.Pets
        .Where(p => p.Id != sourcePetId)
        .Where(p => p.LifeState == PetLifeState.Awake)
        .Where(p => p.IsVisible && p.ChatEnabled)
        .Where(p => p.ActivityState != PetActivityState.Thinking)
        .ToList();

    if (candidates.Count == 0) return;

    var target = random.PickOne(candidates);
    if (target.ActivityState == PetActivityState.Thinking) return;

    await relayService.SendAsync(sourcePetId, target.Id, sourceResponse);
}
```

## 18. セキュリティ

- `.codex\pets` は読取専用で扱う。
- APIキー/トークンをログへ出力しない。
- 秘密情報は DPAPI 等 Windows ユーザーコンテキストで暗号化して保存する。
- Persona や Relay 内容はユーザーのローカルデータとして扱う。
- チャット送信前に利用BackendをUIで明示する。
- 外部URLを開く場合は `http/https` のみ許可する。
- WebView/DOM自動操作はv0.1対象外。

## 19. エラー処理

| 事象 | 動作 |
|---|---|
| pet.json 不正 | 当該ペットのみ無効化し、一覧にエラー表示 |
| スプライト読込失敗 | 当該ペットのみ placeholder またはエラー表示 |
| Backend未設定 | ChatWindow に設定誘導を表示。Pet表示は継続 |
| APIエラー | 失敗メッセージを履歴へSystemEventとして記録し、他Petへ影響させない |
| Relay先APIエラー | RelayEventをFailedで記録。再Relay/再抽選しない |
| DB書込失敗 | UI通知とログ。可能なら会話テキストをメモリ上で保持 |
| 設定JSON破損 | バックアップから復旧を試み、失敗時は初期設定を生成 |
| 画面外座標 | 起動時に可視ディスプレイ領域へ補正 |

## 20. 非機能要件

- Windows 11 を主対象とする。
- .NET 10 を基本とする。
- 起動後、ペット数が少数〜十数体程度で常駐可能な負荷を目標とする。
- Idle時はアニメーション以外のCPU負荷を極力発生させない。
- UIスレッドでネットワーク待ちを行わない。
- ペット1匹/Backend1つの障害をアプリ全体へ波及させない。
- ログはローテーションし、秘密情報を含めない。
- 設定ファイルは人間が読める形式とする。

## 21. 推奨技術

- .NET 10 / C#
- WPF
- MVVM
- `Microsoft.Data.Sqlite`
- WebP/スプライト処理: SkiaSharp 等を候補とする
- JSON: `System.Text.Json`
- DI/Hosting: `Microsoft.Extensions.Hosting` を必要最小限で使用
- Logging: `Microsoft.Extensions.Logging`
- Secret: DPAPI (`ProtectedData`) または同等のWindowsユーザー保護

依存パッケージは必要最小限にし、ペット表示のみで巨大なフレームワークを導入しない。

## 22. 推奨プロジェクト構成

```text
src/
├─ AiPetGarden.App/
│  ├─ Views/
│  ├─ ViewModels/
│  └─ App.xaml
├─ AiPetGarden.Core/
│  ├─ Pets/
│  ├─ Chat/
│  ├─ Relay/
│  ├─ Profiles/
│  └─ Models/
├─ AiPetGarden.Infrastructure/
│  ├─ Persistence/
│  ├─ Backends/
│  ├─ Secrets/
│  └─ Logging/
└─ AiPetGarden.Tests/
```

3プロジェクト+Tests程度に留め、過剰なClean Architecture化はしない。

## 23. v0.1 実装順序

1. `.codex\pets` 検出とペット一覧表示
2. 単体 PetWindow + Idle アニメーション
3. 複数 PetWindow + 移動/位置保存
4. Awake/Sleeping/Hidden
5. PetProfile と設定画面
6. ChatWindow とダミー Backend
7. 実 Backend 1種類
8. SQLite 会話履歴
9. RelayManager
10. エラー処理/SecretStore/ログ
11. 結合テスト
12. 配布ビルド

## 24. 受入条件

### AC-01 ペット読込
正常なペットを2体以上読み込み、同時表示できる。

### AC-02 独立位置
各ペットを別位置へ移動し、再起動後も位置が復元される。

### AC-03 独立チャット
2体のペットで別々の会話を行い、履歴が混ざらない。

### AC-04 Persona
同一質問を2体へ送った場合、それぞれのSystem Promptを使用したリクエストが送られる。

### AC-05 Relay Hit
Relay確率を100%に設定し、起きている別ペット1体へ回答が転送され、そのペットが反応する。

### AC-06 Relay Miss
Relay確率を0%に設定した場合、Relayが一度も発生しない。

### AC-07 No Chain
Relay応答から別ペットへRelayが発生しない。

### AC-08 Sleep Exclusion
Sleeping/Hidden のペットはRelayターゲットに選ばれない。

### AC-09 Busy Exclusion
Thinking中のペットはRelayターゲットに選ばれない。

### AC-10 Fault Isolation
1体のBackendを故意に失敗させても、他ペットの表示/チャットが継続する。

### AC-11 Secret
保存済み設定JSON/DB/ログを確認してもAPIキーの平文が存在しない。

### AC-12 Codex Asset Safety
アプリ操作前後で `.codex\pets` 配下のファイルが更新されていない。

## 25. テスト方針

### Unit Test
- Relay確率境界 (0%, 100%)
- Candidate filter
- No Chain
- PetProfile serialize/deserialize
- Sprite adapter selection
- ProjectRef URL validation

### Integration Test
- 複数ペット読込
- SQLite保存/復元
- FakeChatBackend による会話/Relay
- Backend timeout/cancel
- 設定破損時復旧

### UI Test / 手動確認
- 透明ウィンドウのクリック/ドラッグ
- マルチモニタ座標
- DPI 100/125/150/200%
- TopMost ON/OFF
- 画面外復旧
- ペット数増加時のCPU/GPU負荷

## 26. 将来拡張

- ChatGPT Project へ直接接続できる Backend
- 複数プロバイダー (OpenAI / Gemini / Claude / Local LLM)
- ペットごとのRelay送信率/受信率
- 話題キーワードによるRelay補正
- 発言吹き出し/未読バッジ
- ペット徘徊/近接イベント
- ペット同士の明示的な会話依頼
- 音声合成/音声入力
- 長期記憶/RAG
- セッション/プロジェクト別ペット編成プリセット

## 27. Codex 実装時の指示

- 本文書の v0.1 スコープ外を独断で実装しない。
- `.codex\pets` を書き換えない。
- Relay連鎖を実装しない。
- APIキーを appsettings.json や profile JSON に直接保存しない。
- UI層からHTTP/API SDKを直接呼ばない。
- `IChatBackend` を経由する。
- `RelayManager` は `ChatService` から独立させる。
- まず `FakeChatBackend` でUI/履歴/Relayを完成させてから実APIを接続する。
- 不明な pet metadata を推測で固定実装せず、Adapter/Fallbackで隔離する。
- 追加仕様が必要な場合はコードへ埋め込まず TODO/Issue候補として明示する。

## 28. Definition of Done

v0.1 は、**「複数のCodexペットがデスクトップ上に存在し、各ペットをクリックして固有Personaで会話でき、通常回答の一部がランダムに起きている別ペットへ伝播し、その別ペットが自分のPersonaで一度だけ反応する」**状態をもって完成とする。
