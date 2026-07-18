# Plan

1. Browser正本とWPF現行code/state provenanceを照合する。
2. 旧WPFをexact 100,000 files / multi-folderで測定し、scan、metadata、materialize、UI window、thumbnailの時間と上限を分離する。
3. full-catalog virtualizing panelとfull scroll extentを実装し、Created headerを同panel内のvirtual geometryへ含める。
4. 軽量catalog先行publication、bounded multi-root scan、background metadata、visible-first thumbnail schedulerを実装する。
5. canonical shared project root、gallery aspect、Modal edge/backdrop/control、explicit Enhancement bridgeを実装する。
6. 10,000件の短いgateからexact 100,000件multi-folderへ拡大し、focused回帰とaggregateを実行する。
7. TEMP fixtureで実画面を確認し、仕様、recap、SQLiteを反映してlocal commitする。

## Review rubric

| Axis | Required evidence |
|---|---|
| Completeness | 全件ItemsSource、full extent、末尾index exact、silent truncation 0 |
| Responsiveness | visible container bounded、thumbnail worker bounded、dispatcher liveness green |
| First useful frame | metadata全件完了前にViewerとvisible placeholder/thumbnailが成立 |
| Semantic parity | Created-only section、28/44/28 Modal、backdrop close、Favorite/Seen共有、aspect exact |
| Safety | passive enhancement 0、user source/state/cache/port 3000無変更、TEMP cleanup成功 |
| Regression | scan cancel/race、selection、Delete neighbor、Grid/List、Modal、persistence aggregate green |

100点や完全という表現はこのgateだけでは使わない。greenは今回の再現条件と回帰範囲についての合格であり、standalone Enhancement engineや配布packageは別境界とする。
