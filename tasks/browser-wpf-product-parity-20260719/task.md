# Browser / WPF product-parity completion

## Objective

Browserを製品意味の正本としつつ、ユーザーがこの会話で指定した全機能をBrowser/WPFの両方でほぼ同じ結果になるよう完成させる。DOM/CSS/HTTPとWPF/Windows nativeの機構差は許容するが、操作結果、共有state、Delete安全性、明示Enhancement、性能、復旧、shortcut、設定の意味は揃える。

## Working rules

- `requirements-ledger.md`の各rowを、証拠付き`implemented both`または明示的な`not applicable`へ移すまで落とさない。
- Browserだけ、WPFだけ、古いbranchだけ、testだけを全体完了と報告しない。
- Browser port 3000、ユーザーstate/cache/source、WinForms、deploymentを変更しない。
- Browser runtime testは明示free port 3001、WPF destructive/fault testはTEMP fixtureを使う。
- GitHub Actionsと外部相談をgateにしない。AgmsgはOFF。
- 旧branchはblind cherry-pickせず、current mainへsemantic recoveryする。

## Acceptance

1. `requirements-ledger.md`が会話全体の要望を網羅し、Browser/WPFのlive source/test/runtime証拠を分離する。
2. 変更surfaceごとのfocused test、type/lint/buildまたはRelease buildをgreenにする。
3. Browserはisolated production 3001でDOM、console、layout、interactionを確認する。
4. WPFはfocused verifier、`verify-wpf-product.ps1 -SkipStress`、必要なexact 100,000 gateをgreenにする。
5. shared JSONはversion/validation/unknown preservation/cross-process lock/atomic replace/failure non-mutationを維持する。
6. 最終local mainを通常Browser/WPF launcherへ採用し、revision provenanceを確認する。
7. 正本仕様、truth、成果物、未実装、GitHub/SQLiteを同じrevisionへ揃える。

