using System;
using System.Collections.Generic;
using System.Text;

namespace CarDrawing.Results
{
    /// <summary>
    /// QR 코드 인코더 — 바이트 모드, 오류정정 레벨 M 고정, 버전 1~10 자동 선택 (ISO/IEC 18004).
    /// 결과 처리 시스템에 속하며 QrCodeView가 다운로드 URL을 텍스처로 만들 때 쓴다.
    /// 외부 라이브러리 대신 직접 구현한 이유: 전시장 PC에 배포 의존성을 늘리지 않기 위함 (계획서 9-2).
    /// GCS URL(~100자)은 버전 5~6에 들어가므로 버전 10(213바이트)이면 충분하다.
    /// </summary>
    public static class QrEncoder
    {
        private const int MaxVersion = 10;

        // ── 사양 테이블 (버전 1~10, 오류정정 M) ─────────────────────────
        // 버전별 전체 코드워드 수
        private static readonly int[] TotalCodewords = { 0, 26, 44, 70, 100, 134, 172, 196, 242, 292, 346 };
        // 블록당 오류정정 코드워드 수
        private static readonly int[] EccPerBlock = { 0, 10, 16, 26, 18, 24, 16, 18, 22, 22, 26 };
        // 블록 구성: {1그룹 블록 수, 1그룹 블록당 데이터, 2그룹 블록 수, 2그룹 블록당 데이터}
        private static readonly int[,] BlockLayout =
        {
            { 0, 0, 0, 0 },
            { 1, 16, 0, 0 }, { 1, 28, 0, 0 }, { 1, 44, 0, 0 }, { 2, 32, 0, 0 }, { 2, 43, 0, 0 },
            { 4, 27, 0, 0 }, { 4, 31, 0, 0 }, { 2, 38, 2, 39 }, { 3, 36, 2, 37 }, { 4, 43, 1, 44 }
        };
        // 버전별 정렬 패턴 중심 좌표
        private static readonly int[][] AlignPositions =
        {
            new int[0], new int[0],
            new[] { 6, 18 }, new[] { 6, 22 }, new[] { 6, 26 }, new[] { 6, 30 }, new[] { 6, 34 },
            new[] { 6, 22, 38 }, new[] { 6, 24, 42 }, new[] { 6, 26, 46 }, new[] { 6, 28, 50 }
        };

        /// <summary>
        /// 문자열(UTF-8)을 QR 모듈 행렬로 인코딩한다.
        /// </summary>
        /// <param name="text">인코딩할 문자열 (URL 등)</param>
        /// <param name="forceMask">마스크 강제 지정(0~7). 음수면 사양대로 벌점 최소 마스크를 자동 선택.
        /// 강제 옵션은 외부 구현과 행렬을 1:1 대조하는 검증용이다</param>
        /// <returns>[행, 열] 순서의 모듈 행렬. true = 어두운 모듈</returns>
        public static bool[,] Encode(string text, int forceMask = -1)
        {
            byte[] data = Encoding.UTF8.GetBytes(text);
            int version = ChooseVersion(data.Length);
            byte[] codewords = BuildCodewords(data, version);
            byte[] interleaved = AddEccAndInterleave(codewords, version);
            return BuildMatrix(interleaved, version, forceMask);
        }

        // ── 1) 버전 선택 + 데이터 비트열 구성 ───────────────────────────

        // 바이트 모드 문자 수 지시자 길이: 버전 1~9는 8비트, 10~26은 16비트 (사양 규정)
        private static int CountBits(int version) => version <= 9 ? 8 : 16;

        private static int DataCodewords(int version) =>
            TotalCodewords[version] - EccPerBlock[version] * (BlockLayout[version, 0] + BlockLayout[version, 2]);

        private static int ChooseVersion(int byteLength)
        {
            for (int v = 1; v <= MaxVersion; v++)
            {
                if (4 + CountBits(v) + 8 * byteLength <= DataCodewords(v) * 8) return v;
            }
            throw new ArgumentException($"QR 용량 초과: {byteLength}바이트 (버전 {MaxVersion}-M 한계 {DataCodewords(MaxVersion) - 3}바이트)");
        }

        private static byte[] BuildCodewords(byte[] data, int version)
        {
            int capacityBits = DataCodewords(version) * 8;
            var buf = new List<byte>();
            int bitLen = 0;

            AppendBits(buf, ref bitLen, 0b0100, 4); // 바이트 모드 지시자
            AppendBits(buf, ref bitLen, data.Length, CountBits(version));
            foreach (byte b in data) AppendBits(buf, ref bitLen, b, 8);

            // 종결자(최대 4비트) → 바이트 경계 정렬 → 0xEC/0x11 교대 패딩 (모두 사양 규정)
            AppendBits(buf, ref bitLen, 0, Math.Min(4, capacityBits - bitLen));
            if (bitLen % 8 != 0) AppendBits(buf, ref bitLen, 0, 8 - bitLen % 8);
            for (byte pad = 0xEC; bitLen < capacityBits; pad ^= 0b11111101) // 0xEC ↔ 0x11 교대
                AppendBits(buf, ref bitLen, pad, 8);

            return buf.ToArray();
        }

        private static void AppendBits(List<byte> buf, ref int bitLen, int value, int count)
        {
            for (int i = count - 1; i >= 0; i--)
            {
                if (bitLen % 8 == 0) buf.Add(0);
                if (((value >> i) & 1) != 0) buf[bitLen / 8] |= (byte)(0x80 >> (bitLen % 8));
                bitLen++;
            }
        }

        // ── 2) Reed-Solomon 오류정정 + 블록 인터리브 ────────────────────

        private static byte[] AddEccAndInterleave(byte[] data, int version)
        {
            int eccLen = EccPerBlock[version];
            byte[] divisor = RsDivisor(eccLen);
            var dataBlocks = new List<byte[]>();
            var eccBlocks = new List<byte[]>();

            int offset = 0;
            for (int group = 0; group < 2; group++)
            {
                int blockCount = BlockLayout[version, group * 2];
                int blockLen = BlockLayout[version, group * 2 + 1];
                for (int b = 0; b < blockCount; b++)
                {
                    var block = new byte[blockLen];
                    Array.Copy(data, offset, block, 0, blockLen);
                    dataBlocks.Add(block);
                    eccBlocks.Add(RsRemainder(block, divisor));
                    offset += blockLen;
                }
            }

            // 인터리브: 각 블록의 i번째 데이터 코드워드를 순서대로, 이어서 오류정정 코드워드를 같은 방식으로
            var result = new byte[TotalCodewords[version]];
            int k = 0;
            int maxLen = dataBlocks[dataBlocks.Count - 1].Length; // 2그룹 블록이 1그룹보다 1바이트 길다
            for (int i = 0; i < maxLen; i++)
                foreach (byte[] block in dataBlocks)
                    if (i < block.Length) result[k++] = block[i];
            for (int i = 0; i < eccLen; i++)
                foreach (byte[] block in eccBlocks) result[k++] = block[i];
            return result;
        }

        // GF(2^8) 곱셈 (기약다항식 0x11D — QR 사양 고정값)
        private static int GfMul(int a, int b)
        {
            int z = 0;
            for (int i = 7; i >= 0; i--)
            {
                z = (z << 1) ^ ((z >> 7) * 0x11D);
                z ^= ((b >> i) & 1) * a;
            }
            return z;
        }

        // 차수 degree의 RS 생성 다항식 계수 (최고차항 1 생략 저장)
        private static byte[] RsDivisor(int degree)
        {
            var result = new byte[degree];
            result[degree - 1] = 1;
            int root = 1;
            for (int i = 0; i < degree; i++)
            {
                for (int j = 0; j < degree; j++)
                {
                    result[j] = (byte)GfMul(result[j], root);
                    if (j + 1 < degree) result[j] ^= result[j + 1];
                }
                root = GfMul(root, 0x02); // 생성원 α=2의 거듭제곱
            }
            return result;
        }

        // data를 생성 다항식으로 나눈 나머지 = 오류정정 코드워드
        private static byte[] RsRemainder(byte[] data, byte[] divisor)
        {
            var result = new byte[divisor.Length];
            foreach (byte b in data)
            {
                int factor = b ^ result[0];
                Array.Copy(result, 1, result, 0, result.Length - 1);
                result[result.Length - 1] = 0;
                for (int j = 0; j < result.Length; j++)
                    result[j] ^= (byte)GfMul(divisor[j], factor);
            }
            return result;
        }

        // ── 3) 행렬 배치 ────────────────────────────────────────────────

        // 모듈 행렬 + 기능 패턴 표시. 기능 영역은 데이터 배치·마스킹에서 제외해야 해서 함께 기록한다
        private sealed class Matrix
        {
            public readonly int Size;
            public readonly bool[,] Modules;    // [y, x], true = 어두움
            public readonly bool[,] IsFunction; // 기능 패턴(파인더·타이밍·포맷 등) 여부

            public Matrix(int version)
            {
                Size = version * 4 + 17;
                Modules = new bool[Size, Size];
                IsFunction = new bool[Size, Size];
            }

            public void Set(int x, int y, bool dark)
            {
                Modules[y, x] = dark;
                IsFunction[y, x] = true;
            }
        }

        private static bool GetBit(int value, int i) => ((value >> i) & 1) != 0;

        private static bool[,] BuildMatrix(byte[] codewords, int version, int forceMask)
        {
            var m = new Matrix(version);
            DrawFunctionPatterns(m, version);
            DrawCodewords(m, codewords);

            int mask = forceMask;
            if (mask < 0)
            {
                // 사양대로 8개 마스크를 모두 적용해 벌점이 가장 낮은 것을 고른다 (스캔 안정성)
                long best = long.MaxValue;
                for (int i = 0; i < 8; i++)
                {
                    ApplyMask(m, i);
                    DrawFormatBits(m, i);
                    long penalty = PenaltyScore(m);
                    if (penalty < best) { best = penalty; mask = i; }
                    ApplyMask(m, i); // XOR 연산이라 재적용이 곧 원복
                }
            }
            ApplyMask(m, mask);
            DrawFormatBits(m, mask);
            return m.Modules;
        }

        private static void DrawFunctionPatterns(Matrix m, int version)
        {
            // 타이밍 패턴 (파인더가 이후 덮어쓴다)
            for (int i = 0; i < m.Size; i++)
            {
                m.Set(6, i, i % 2 == 0);
                m.Set(i, 6, i % 2 == 0);
            }

            // 파인더 3개 (분리자 포함 — dist 4가 밝은 테두리 역할)
            DrawFinder(m, 3, 3);
            DrawFinder(m, m.Size - 4, 3);
            DrawFinder(m, 3, m.Size - 4);

            // 정렬 패턴 (파인더와 겹치는 세 모서리는 제외)
            int[] pos = AlignPositions[version];
            for (int i = 0; i < pos.Length; i++)
                for (int j = 0; j < pos.Length; j++)
                {
                    if ((i == 0 && j == 0) || (i == 0 && j == pos.Length - 1) || (i == pos.Length - 1 && j == 0))
                        continue;
                    DrawAlignment(m, pos[i], pos[j]);
                }

            // 포맷 정보 자리를 기능 영역으로 선점 (실제 값은 마스크 확정 후 다시 그린다)
            DrawFormatBits(m, 0);

            // 버전 정보 (버전 7 이상만 존재)
            if (version >= 7) DrawVersionBits(m, version);
        }

        private static void DrawFinder(Matrix m, int cx, int cy)
        {
            for (int dy = -4; dy <= 4; dy++)
                for (int dx = -4; dx <= 4; dx++)
                {
                    int x = cx + dx, y = cy + dy;
                    if (x < 0 || x >= m.Size || y < 0 || y >= m.Size) continue;
                    int dist = Math.Max(Math.Abs(dx), Math.Abs(dy));
                    m.Set(x, y, dist != 2 && dist != 4); // 링 2·4가 밝은 띠
                }
        }

        private static void DrawAlignment(Matrix m, int cx, int cy)
        {
            for (int dy = -2; dy <= 2; dy++)
                for (int dx = -2; dx <= 2; dx++)
                    m.Set(cx + dx, cy + dy, Math.Max(Math.Abs(dx), Math.Abs(dy)) != 1);
        }

        private static void DrawFormatBits(Matrix m, int mask)
        {
            // 5비트(오류정정 M=00 + 마스크 3비트)를 BCH(15,5)로 확장 후 고정 패턴과 XOR
            int data = (0b00 << 3) | mask;
            int rem = data;
            for (int i = 0; i < 10; i++) rem = (rem << 1) ^ ((rem >> 9) * 0x537);
            int bits = ((data << 10) | rem) ^ 0x5412;

            // 사본 1: 좌상단 파인더 주변
            for (int i = 0; i <= 5; i++) m.Set(8, i, GetBit(bits, i));
            m.Set(8, 7, GetBit(bits, 6));
            m.Set(8, 8, GetBit(bits, 7));
            m.Set(7, 8, GetBit(bits, 8));
            for (int i = 9; i < 15; i++) m.Set(14 - i, 8, GetBit(bits, i));

            // 사본 2: 우상단·좌하단 파인더 주변
            for (int i = 0; i < 8; i++) m.Set(m.Size - 1 - i, 8, GetBit(bits, i));
            for (int i = 8; i < 15; i++) m.Set(8, m.Size - 15 + i, GetBit(bits, i));
            m.Set(8, m.Size - 8, true); // 다크 모듈 (항상 어두움)
        }

        private static void DrawVersionBits(Matrix m, int version)
        {
            // 6비트 버전을 BCH(18,6)로 확장
            int rem = version;
            for (int i = 0; i < 12; i++) rem = (rem << 1) ^ ((rem >> 11) * 0x1F25);
            int bits = (version << 12) | rem;
            for (int i = 0; i < 18; i++)
            {
                bool bit = GetBit(bits, i);
                int a = m.Size - 11 + i % 3, b = i / 3;
                m.Set(a, b, bit); // 우상단 3×6
                m.Set(b, a, bit); // 좌하단 6×3
            }
        }

        private static void DrawCodewords(Matrix m, byte[] codewords)
        {
            // 우하단부터 2열씩 지그재그로 채운다. 세로 타이밍 열(x=6)은 건너뛴다 (사양 배치 규칙)
            int i = 0;
            for (int right = m.Size - 1; right >= 1; right -= 2)
            {
                if (right == 6) right = 5;
                for (int vert = 0; vert < m.Size; vert++)
                {
                    for (int j = 0; j < 2; j++)
                    {
                        int x = right - j;
                        bool upward = ((right + 1) & 2) == 0;
                        int y = upward ? m.Size - 1 - vert : vert;
                        if (m.IsFunction[y, x] || i >= codewords.Length * 8) continue;
                        m.Modules[y, x] = GetBit(codewords[i >> 3], 7 - (i & 7));
                        i++;
                    }
                }
            }
        }

        private static void ApplyMask(Matrix m, int mask)
        {
            for (int y = 0; y < m.Size; y++)
                for (int x = 0; x < m.Size; x++)
                {
                    if (m.IsFunction[y, x]) continue;
                    bool invert;
                    switch (mask) // 사양의 8개 마스크 조건식
                    {
                        case 0: invert = (x + y) % 2 == 0; break;
                        case 1: invert = y % 2 == 0; break;
                        case 2: invert = x % 3 == 0; break;
                        case 3: invert = (x + y) % 3 == 0; break;
                        case 4: invert = (x / 3 + y / 2) % 2 == 0; break;
                        case 5: invert = x * y % 2 + x * y % 3 == 0; break;
                        case 6: invert = (x * y % 2 + x * y % 3) % 2 == 0; break;
                        default: invert = ((x + y) % 2 + x * y % 3) % 2 == 0; break;
                    }
                    m.Modules[y, x] ^= invert;
                }
        }

        // 마스크 선택용 벌점 (사양 4개 규칙: 연속 런, 2×2 블록, 파인더 유사 패턴, 명암 비율)
        private static long PenaltyScore(Matrix m)
        {
            long result = 0;
            int size = m.Size;

            // 규칙 1+3: 행 방향 (연속 5개 이상 +3/+1, 파인더 유사 11비트 패턴 +40)
            for (int y = 0; y < size; y++)
            {
                bool runColor = false;
                int runLen = 0, window = 0;
                for (int x = 0; x < size; x++)
                {
                    bool c = m.Modules[y, x];
                    if (x == 0 || c != runColor) { runColor = c; runLen = 1; }
                    else
                    {
                        runLen++;
                        if (runLen == 5) result += 3;
                        else if (runLen > 5) result++;
                    }
                    window = ((window << 1) & 0x7FF) | (c ? 1 : 0);
                    if (x >= 10 && (window == 0x5D0 || window == 0x05D)) result += 40; // 1011101 + 0000 양방향
                }
            }
            // 규칙 1+3: 열 방향
            for (int x = 0; x < size; x++)
            {
                bool runColor = false;
                int runLen = 0, window = 0;
                for (int y = 0; y < size; y++)
                {
                    bool c = m.Modules[y, x];
                    if (y == 0 || c != runColor) { runColor = c; runLen = 1; }
                    else
                    {
                        runLen++;
                        if (runLen == 5) result += 3;
                        else if (runLen > 5) result++;
                    }
                    window = ((window << 1) & 0x7FF) | (c ? 1 : 0);
                    if (y >= 10 && (window == 0x5D0 || window == 0x05D)) result += 40;
                }
            }

            // 규칙 2: 같은 색 2×2 블록
            for (int y = 0; y < size - 1; y++)
                for (int x = 0; x < size - 1; x++)
                {
                    bool c = m.Modules[y, x];
                    if (c == m.Modules[y, x + 1] && c == m.Modules[y + 1, x] && c == m.Modules[y + 1, x + 1])
                        result += 3;
                }

            // 규칙 4: 어두운 모듈 비율의 50% 이탈 정도
            int dark = 0;
            foreach (bool cell in m.Modules) if (cell) dark++;
            int total = size * size;
            int k = (Math.Abs(dark * 20 - total * 10) + total - 1) / total - 1;
            result += k * 10;
            return result;
        }
    }
}
