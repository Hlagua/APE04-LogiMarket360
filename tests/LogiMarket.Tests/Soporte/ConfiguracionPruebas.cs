// MetricasSesion es un Singleton real (una sola instancia por proceso). Las pruebas que verifican sus
// contadores no pueden correr en paralelo: cada EntornoPrueba la reinicia antes de empezar.
[assembly: Xunit.CollectionBehavior(DisableTestParallelization = true)]
