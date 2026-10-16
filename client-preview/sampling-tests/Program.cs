using WeekendDropsUI.Services;
int passed=0;
void Check(bool value,string name){if(!value)throw new Exception(name);passed++;}
Check(NativeAudio.ShouldSampleLootAt(0,1),"First sample must be immediate");
Check(!NativeAudio.ShouldSampleLootAt(0,1),"Duplicate clock sample");
Check(!NativeAudio.ShouldSampleLootAt(0.499f,1),"Early sample");
Check(NativeAudio.ShouldSampleLootAt(0.5f,1),"Boundary sample");
Check(NativeAudio.ShouldSampleLootAt(0.6f,2),"New raid must sample immediately");
Check(NativeAudio.ShouldSampleLootAt(0,2),"Clock rollback must recover");
Check(!NativeAudio.ShouldSampleLootAt(float.NaN,2),"NaN rejected");
Check(!NativeAudio.ShouldSampleLootAt(float.PositiveInfinity,2),"Infinity rejected");
Check(NativeAudio.ShouldSampleLootAt(10,2),"Long pause must sample");
Check(!NativeAudio.ShouldSampleLootAt(10,2),"No catch-up burst");
foreach(int fps in new[]{30,60,144}){
 int count=0;for(int frame=0;frame<fps*60;frame++)if(NativeAudio.ShouldSampleLootAt(100+frame/(float)fps,1000+fps))count++;
 Check(count<=120&&count>=110,"Sampling rate at "+fps+" FPS: "+count);
 Console.WriteLine($"Simulated {fps} FPS for 60 s: {fps*60} updates -> {count} inventory reads.");
}
Console.WriteLine($"Passed: {passed}, Failed: 0. Simulated cadence only, not measured game FPS.");
