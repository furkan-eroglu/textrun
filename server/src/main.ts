// Nakama'nın yüklediği giriş noktası. Sunucu başlarken InitModule bir kez çağrılır.
// Kaydedilen her fonksiyon burada "export" edilmelidir (bkz. build.mjs).
export function pingRpc(
  ctx: nkruntime.Context,
  logger: nkruntime.Logger,
  nk: nkruntime.Nakama,
  payload: string
): string {
  return JSON.stringify({
    ok: true,
    message: "TextRun sunucusu calisiyor",
    time: Date.now(),
  });
}

export function InitModule(
  ctx: nkruntime.Context,
  logger: nkruntime.Logger,
  nk: nkruntime.Nakama,
  initializer: nkruntime.Initializer
): void {
  initializer.registerRpc("ping", pingRpc);
  logger.info("TextRun modulu yuklendi");
}
