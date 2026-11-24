# Hero's Adventure

## 📖 Giới thiệu (Introduction)
**Hero's Adventure** là tựa game nhập vai chiến thuật theo lượt (**2D Tactical Match-3 RPG**) được phát triển trên **Unity (`2022.3.62f3`)**. Người chơi vào vai một chiến binh khám phá các vùng đất, đối đầu với nhiều chủng loại quái vật và Boss thông qua cơ chế **hoán đổi ngọc trên bàn cờ dùng chung 8×8 (Shared-Board Swap & Cascade)** kết hợp cùng hệ thống nâng cấp chỉ số, thiên phú và trang bị chuyên sâu.

---

## 🎮 Các tính năng nổi bật (Key Features)
- **Bàn cờ Chiến thuật Dùng Chung 8×8 (Shared-Board Match-3)**: Người chơi và AI quái vật cùng tương tác trên một bàn cờ duy nhất — mỗi nước đi vừa tạo combo cho bản thân vừa có thể phá thế tấn công của đối phương.
- **Hai chế độ chơi (Game Modes)**:
  - **Level Mode (5 Ải Chiến Dịch & Đánh giá 1–3 Sao)**: Vượt qua từng đợt quái (*Slime, Skeleton, Orc*) để chạm trán **Boss Golem** ở Level 5. Hỗ trợ thưởng vượt ải lần đầu, thưởng cày lại (`35%`) và thưởng bù Kim Cương khi cải thiện số Sao.
  - **Endless Mode (Sinh Tồn Vô Tận)**: Độ khó tăng tiến độc lập theo từng chỉ số (`+14% HP`, `+8% DMG`, `+10% Shield` mỗi Round), chạm trán **Boss Golem mỗi 5 Round**, thưởng lũy tiến và nhận thêm `+1 Kim Cương` cho mỗi cột mốc 5 Round vượt qua.
- **Nhịp độ khám phá linh hoạt**: Di chuyển chuyển cảnh mượt mà (`1.5–2.2s`), tích hợp nút tăng tốc chiến đấu **`1x / 2x`**, và `25%` cơ hội bắt gặp sự kiện ngẫu nhiên giữa các đợt quái (*Healing Spring, Ancient Shrine, Treasure Chest*).
- **Hệ thống Tiến trình & Kinh tế hoàn chỉnh (5 Tab Menu)**: Thăng cấp Anh hùng bằng EXP, nâng cấp 3 nhánh **Talent**, sưu tầm & cường hóa 12 **Equipment** thuộc 6 ô trang bị, mua sắm tại **Shop** và nhận thưởng thành tựu tại **Reward**.
- **Trải nghiệm Nghe – Nhìn tối ưu (Game Feel & Audio)**: Thanh máu + Khiên trực tiếp dưới chân nhân vật (`CharacterWorldHUD`), số nổi phân màu dùng Object Pool, hiệu ứng nổ hạt 2D + Hit-Stop + rung phản hồi (`CombatVFXManager`), và hệ thống âm thanh đa kênh hỗ trợ pitch tăng dần theo chuỗi combo & tự động giảm âm nhạc nền khi tạm dừng (`AudioManager`).

---

## ⚔️ Cơ chế Chiến đấu & Giải đố (Combat & Match-3 Mechanics)

### 1. Luật Hoán Đổi, Nổ Đặc Biệt & Cascade
- **Hoán đổi 4 hướng (`1 AP / lượt`)**: Kéo hoặc chạm chọn 2 viên ngọc liền kề theo chiều ngang/dọc để ghép thành hàng hoặc cột từ **3 viên cùng loại trở lên**.
- **Nổ Hàng / Cột (Match-4)**: Ghép 4 viên thẳng hàng sẽ quét nổ toàn bộ hàng hoặc cột tương ứng.
- **Nổ Chữ Thập — Cross Blast (Match-5+ / Hình L, T, +)**: Ghép từ 5 viên trở lên hoặc tạo giao điểm chữ L, T, + sẽ kích hoạt nổ chữ thập quét sạch **cả hàng lẫn cột** đi qua tâm (`0.35×` hiệu lực cho mỗi viên nổ lan).
- **Hệ số Combo & Giảm biên độ Cascade**:
  - Ghép nhiều cụm trong cùng một lượt cộng thêm **`+10%` hiệu lực mỗi cụm** (trần tối đa **`1.45×`**).
  - Cụm ghép trực tiếp đạt **`100%`** giá trị; các nhịp ngọc rơi tự động (**Cascade**) giảm dần biên độ theo bậc **`65% → 45% → 30%`** để giữ cân bằng chiến thuật.

### 2. Hệ thống 6 Loại Ngọc trên Bàn Cờ
| Loại ngọc | Vai trò & Công thức cơ bản | Cơ chế đặc biệt |
| :--- | :--- | :--- |
| 🗡️ **Damage Gem** | Gây sát thương vật lý (trọng số xuất hiện `2`) | Ghép **≥ 2 cụm Damage** hoặc **Match-4+** kích hoạt **Đánh Lan (`Overkill Cleave`)** truyền `50%` sát thương dư sang tối đa 2 mục tiêu kế tiếp |
| 💖 **Health Gem** | Hồi `5 + 8% MaxHP` | Hồi phục máu thực tế, trực tiếp quyết định mốc đánh giá **1–3 Sao** cuối trận |
| 🛡️ **Shield Gem** | Tạo Khiên `6 + 10% MaxHP` | **Pure Barrier 1:1** hấp thụ sát thương trước khi trừ vào HP (trần `50% MaxHP`, tự hao hụt `25%` mỗi đầu lượt) |
| 🎯 **Crit Rate Gem** | Tăng `+5%` Tỉ lệ Bạo kích | Tồn tại **4 lượt** (trần cộng thêm `+15%`); tự xả `50%` lượng cộng khi nổ Chí mạng |
| 💥 **Crit Damage Gem** | Tăng `+10%` Sát thương Bạo kích | Tồn tại **4 lượt** (trần cộng thêm `+30%`); tự xả `50%` lượng cộng khi nổ Chí mạng |
| 💨 **Dodge Gem** | Tăng `+3%` Tỉ lệ Né tránh | Tồn tại **4 lượt** (trần cộng thêm `+12%`); tự xả `50%` lượng cộng khi Né đòn thành công |

> **Cơ chế Chuyển hóa Vượt Trần (Buff Overflow Conversion)**: Khi các chỉ số `Crit Rate`, `Crit Damage` hoặc `Dodge` đã chạm trần cộng thêm trong trận, việc ghép tiếp các ngọc này không bị lãng phí mà tự động chuyển hóa phần dư thành **Sát thương phụ** hoặc **Khiên bảo vệ**. Khi qua đợt quái mới (Wave), lượng buff tạm thời giảm nhẹ `50%`.

### 3. Lượt Địch & Phối Hợp Đánh Phụ (Enemy Turn & Support Strikes)
- **Quái chủ công xoay vòng**: Mỗi lượt địch có 1 quái chủ công thực hiện nước đi ưu tiên trên bàn cờ. Nếu trên bàn không có nước ghép ngọc Sát thương nào, quái chủ công vẫn áp sát tung đòn đánh thường cơ bản (`0.65×` Damage).
- **Đòn đánh bồi theo nhịp (`Support Strikes`)**: Khi có nhiều quái cùng đứng trên sân, các quái phụ sẽ đếm ngược hồi chiêu (`2–3` lượt) để lao lên tung đòn đánh phụ (`0.50×` Damage), buộc người chơi phải tính toán thứ tự tiêu diệt hợp lý.

---

## 📈 Hệ thống Tiến trình & Kinh tế (Meta-Progression)
- **Cấp độ Anh hùng (`Player Level`)**: Tích lũy EXP từ vượt ải và Shop để tự động cộng thêm HP, Sát thương và Khiên nền mỗi cấp.
- **Thiên phú (`Talent` — nâng bằng Vàng, tối đa Lv.10/nhánh)**:
  1. *Attack Mastery*: `+1.5 DMG` & `+0.5% Crit Rate` mỗi cấp.
  2. *Vitality & Guard*: `+14 MaxHP` & `+2 Shield` mỗi cấp.
  3. *Gem Mastery*: `+5%` hiệu lực ngọc *Damage / Health / Shield* mỗi cấp.
- **Trang bị (`Equipment` — 12 món chia 6 ô: *Weapon, Armor, Helmet, Boots, Ring, Amulet*)**:
  - Gồm 3 phẩm chất (*Common, Rare, Epic*), cường hóa từ **`Lv.1 → Lv.5`** (`+25%` chỉ số mỗi cấp).
  - Sở hữu các nội tại chiến thuật như **Khiên đầu trận (`StartBattleShield`)** trên dòng *Charm/Amulet* hoặc **Hồi máu khi ghép Match-4+ (`Match4BonusHeal`)** trên *Phoenix Ring*.
- **Cửa hàng (`Shop`) & Thành tựu (`Reward`)**:
  - Mở *Iron Chest* bằng Vàng, *Epic Chest* bằng Kim Cương (tự động quy đổi mảnh trùng thành Vàng hỗ trợ nâng cấp), hoặc mua trang bị chỉ định và gói tài nguyên.
  - Nhận thưởng Vàng, Kim Cương và trang bị hiếm khi đạt **8 cột mốc** tổng Sao chiến dịch (3 → 15 Sao) và kỷ lục Endless (Round 3, 5, 10).

---

## 🚀 Hướng dẫn Cách chơi (How to Play)
1. **Chuẩn bị tại Menu chính**: Kiểm tra các tab **Talent**, **Equip**, **Shop** và **Reward** để tối ưu chỉ số trước khi xuất trận, sau đó vào tab **Battle** chọn màn chơi hoặc chế độ **Endless**.
2. **Ghép ngọc trên bàn cờ 8×8**: Kéo thả hoặc chạm 2 viên ngọc kề nhau để tạo cụm ≥ 3 viên. Ưu tiên tạo **Match-4** và **Match-5+ (Cross Blast)** để dọn bàn cờ và tích lũy combo.
3. **Chọn mục tiêu tấn công**: Khi có nhiều quái vật trên sân và bạn ghép trúng **Damage Gem**, hãy chạm trực tiếp vào kẻ địch muốn dồn sát thương (hoặc tận dụng **Overkill Cleave** để kết liễu mục tiêu thấp máu và lan sát thương sang quái kế bên).
4. **Quản trị Khiên để giữ 3 Sao**: Hãy dựng **Shield Gem** trước các đòn đánh của *Orc* hoặc **Boss Golem** để bảo toàn thanh máu thực và đạt đánh giá **3 Sao** tối đa.

---

## 🛠️ Kiến trúc Kỹ thuật & Công cụ (Architecture & Tooling)

### Cấu trúc Thư mục Chính (`Assets/Scripts/`)
- `Character/`: `BaseCharacter`, `Player`, `Enemy`, `EnemySelectionManager` — quản lý chỉ số, Khiên 1:1, Buff theo lượt và chọn mục tiêu.
- `GameGrid/`: `GameGrid`, `GridInput`, `Gem`, `GemType` — thuật toán sinh bàn cờ không trùng sẵn, kiểm tra nước đi khả thi, nổ dây chuyền và hiệu ứng DOTween.
- `Manager/`: `BattleManager`, `AudioManager`, `CombatVFXManager`, `PoolingManager`, `ObserverManager`, `NavigationManager` & `GameMode/` (`IGameModeStrategy`, `LevelModeStrategy`, `EndlessModeStrategy`).
- `Progression/`: `MetaProgressionSystem`, `BalanceConfigSO` (hỗ trợ xuất/nạp JSON tương thích *Unity Remote Config*), `CombatSimulator` (mô phỏng trận đấu tốc độ cao không phụ thuộc khung hình).
- `SaveLoad/`: `DataManager` — lưu trữ tiến trình màn chơi, kỷ lục Endless, trang bị, thiên phú và cài đặt âm thanh qua JSON cục bộ.
- `UI/`: `GameplayUIManager`, `CharacterWorldHUD`, `VictoryUI`, `DefeatUI`, `PausePopup` và bộ điều khiển `UI/Meta/` (`TalentTabController`, `EquipmentTabController`, `ShopTabController`, `RewardTabController`).
- `Editor/`: `BalanceSimulatorWindow` — công cụ kiểm thử cân bằng trực tiếp trong Unity Editor.

### Công cụ Mô phỏng Cân bằng (Editor Balance Simulator)
Mở trên thanh menu Unity tại **`Tools > Hero Adventure > Balance Simulator`** (xây dựng bằng **UI Toolkit**):
- Chạy hàng nghìn trận mô phỏng **Monte-Carlo** cho từng màn chơi hoặc chế độ Endless với các cấu hình cấp độ, Talent và Trang bị tùy chỉnh.
- Thống kê trực quan **Win Rate (%)**, **Tỉ lệ đạt 3 Sao (%)**, **Số lượt trung bình** và hỗ trợ tinh chỉnh/xuất cấu hình `BalanceConfigSO` ra JSON.

### Công nghệ & Design Patterns
- **Engine**: Unity `2022.3.62f3` (C#, Built-in Render Pipeline 2D, uGUI `1080×1920`, TextMeshPro, UI Toolkit cho Editor Tool, DOTween).
- **Design Patterns**: Singleton, Observer (`EventID`), Object Pooling (`PoolingManager` & `UnityEngine.Pool.ObjectPool`), Strategy (`IGameModeStrategy`), Data-Driven ScriptableObjects.
