# Browser / WPF Product Perfection Audit

## Objective

現行Browserを製品仕様の正本として実機・DOM・fault・大規模catalogまで再監査し、WPFを同じ主要workflowへ揃えつつ、Browserの限界を超えるnative品質へ近づける。

## Boundaries

- user rootのport 3000、既存state/cache、unrelated dirty fileは変更しない。
- Browser検証はintegrated worktreeのloopback isolated runtimeを使う。
- WPF検証はTEMP-only fixtureと明示的なstate path overrideを使う。
- deployment、public packaging、local-native以外のnative製品、外部AI相談は行わない。
- GitHub Actionsはgateにせず、local verifierを正本とする。

## Review axes

1. データ非破壊
2. 操作正確性
3. 回帰耐性
4. 大規模性能
5. 失敗回復
6. Accessibility
7. 起動終了品質
8. Browser意味一致

## Required evidence

- Browser unit/typecheck/lint/build/Playwright/UI guard
- isolated production runtime provenance、loopback、console 0、current-run screenshots
- Modal Deleteの3枚中央削除 + late refresh統合回帰
- WPF focused fault/race verifier、20,000枚external responsiveness probe、Release aggregate
- user state/cacheとport 3000の最終安全確認
