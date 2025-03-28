# ゲーム翻訳オーバーレイアプリケーション - プロジェクト概要

## 1. プロジェクトの目的

ゲーム翻訳オーバーレイアプリケーションは、ゲーム画面上にリアルタイムで翻訳をオーバーレイ表示するデスクトップアプリケーションです。未翻訳のゲームや言語学習者向けに、ゲームプレイを妨げることなく、画面上のテキストを自動的に認識・翻訳し、元のテキストの近くに表示することで、言語の壁を超えたゲーム体験を可能にします。

## 2. プロジェクト概要

### 2.1 基本機能

- ゲーム画面テキストのリアルタイムOCR（光学文字認識）
- 認識テキストの翻訳処理（ローカル翻訳とAI翻訳）
- 翻訳結果のオーバーレイ表示
- ゲームごとの最適化プロファイル
- AIによるOCR設定の自動最適化

### 2.2 技術的特徴

- PaddleOCRによる高精度なテキスト認識
- OpenAI APIによるオンライン翻訳とLibreTranslateによるローカル翻訳の併用
- OCR最適化のためのAI支援（英語テキスト認識にはGoogle Gemini API、日本語テキスト認識にはOpenAI Vision APIを検討中）
- 差分検出によるパフォーマンス最適化
- クリックスルー可能な透過オーバーレイUI
- Windows Forms（.NET Framework 4.8）による実装

## 3. 基本方針

### 3.1 ユーザー没入体験の重視

ユーザーには本来のコンテンツ（ゲームや電子書籍）に完全に没入してほしいため、アプリケーションの存在感を最小限に抑えます。複雑な設定や調整をユーザーに求めず、AIによる自動最適化を活用して、ユーザーが翻訳アプリを意識することなく使用できる体験を提供します。

### 3.2 幅広い対象アプリケーション

主要なターゲットは未翻訳のゲームですが、電子書籍リーダーなどのPC上で閲覧できる様々なコンテンツも対象とします。アプリケーション固有のUIに依存しない汎用的なテキスト認識・翻訳機能を提供します。

### 3.3 多言語対応

翻訳対象として日本語と英語の間の翻訳をメインとしつつも、他の言語ペアにも対応可能な拡張性の高い設計を採用します。言語自動検出機能により、ユーザーは使用言語を指定することなく利用できます。

## 4. ターゲットユーザー

### 4.1 主要ターゲット

- **外国語ゲームプレイヤー**: 母国語に翻訳されていないゲームを遊びたいユーザー
- **言語学習者**: ゲームを通じて語学を学習したいユーザー
- **コンテンツクリエイター**: 実況やレビュー動画を制作するユーザー

### 4.2 セカンダリターゲット

- **小規模開発者/パブリッシャー**: 専門的なローカライズ予算がない小規模ゲーム開発者
- **翻訳・ローカライズ関係者**: ゲームのローカライズ作業を支援するツールとして
- **研究者/教育者**: 言語教育や研究目的でのツールとして

## 5. プロジェクト構成

### 5.1 ディレクトリ構造

```
app/
├── Core/                      # コアロジック
│   ├── Configuration/        # 設定管理
│   ├── Diagnostics/          # 診断・ログ機能
│   ├── Licensing/            # ライセンス管理
│   ├── OCR/                  # OCR関連機能
│   │   ├── AI/              # AI最適化機能
│   │   ├── Benchmark/       # OCRベンチマーク機能
│   │   └── Utils/          # OCRユーティリティ
│   ├── Security/             # セキュリティ関連
│   ├── Translation/          # 翻訳機能
│   │   ├── Interfaces/      # インターフェース定義
│   │   ├── Models/          # 翻訳モデル
│   │   └── Services/        # 翻訳サービス実装
│   └── Utils/                # 共通ユーティリティ
├── Forms/                     # UI層
│   ├── Controls/             # カスタムコントロール
│   ├── Settings/             # 設定画面関連
│   └── Resources/            # UI用リソース
├── Properties/                # プロジェクトプロパティ
└── Resources/                 # アプリケーションリソース
```

### 5.2 主要コンポーネント

1. **OCR処理コンポーネント**
   - テキスト検出と認識
   - 差分検出による最適化
   - AI支援によるOCR設定の最適化

2. **翻訳処理コンポーネント**
   - ローカル翻訳エンジン連携
   - オンライン翻訳API連携
   - 翻訳キャッシュ管理
   - 言語自動検出

3. **UI/オーバーレイコンポーネント**
   - 透過オーバーレイウィンドウ
   - 翻訳テキスト表示ボックス
   - メイン設定ウィンドウ
   - ゲームウィンドウ選択ツール

4. **セキュリティコンポーネント**
   - APIキーの安全な管理
   - ライセンス管理システム

## 6. 実装状況と開発ロードマップ

### 6.1 現在の実装状況 [2025年3月]

| コンポーネント | 状態 | 備考 |
|--------------|------|------|
| OCR機能 | ほぼ完了 | PaddleOCR統合、差分検出実装済み |
| 翻訳機能 | 実装中 | ローカル翻訳完了、AI翻訳実装中 |
| オーバーレイUI | 実装中 | 基本機能動作、UX改善が必要 |
| 設定・構成 | 実装中 | ゲームプロファイル機能実装中 |
| セキュリティ | 実装中 | APIキー管理実装済み、ライセンス管理開発中 |
| パフォーマンス最適化 | 実装中 | 差分検出による最適化実装済み |

### 6.2 今後の開発計画

#### 短期目標（〜3ヶ月）
- OCR設定の自動最適化機能の完成
- AI翻訳連携の完成
- ゲームプロファイル機能の完成
- パフォーマンス最適化の完了

#### 中期目標（3〜6ヶ月）
- ライセンス管理システムの実装
- UI/UXの改善
- 対応言語の拡充
- 自動更新機能の実装

#### 長期目標（6ヶ月〜）
- コミュニティ機能（プロファイル共有など）
- より高度なAI機能（コンテキスト考慮型翻訳など）
- プラットフォーム拡張（macOS対応検討）

## 7. 関連ドキュメント

- [要件定義書](../02-design/requirements.md) - 詳細な機能要件と非機能要件
- [システムアーキテクチャ仕様書](../02-design/system-architecture.md) - システム設計の詳細
- [OCR処理仕様書](../02-design/components/ocr-component.md) - OCR機能の詳細設計
- [翻訳処理仕様書](../02-design/components/translation-component.md) - 翻訳機能の詳細設計
- [UI設計仕様書](../02-design/components/ui-component.md) - ユーザーインターフェースの設計
- [ビジネスモデル設計書](../05-deployment/business-model.md) - 収益モデルと料金体系
