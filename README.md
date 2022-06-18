
# GPU蒙皮动画

使用GPU来实现蒙皮的运输，节省CPU的开销，一般用于存在大量相同模型的小兵割草类型的战斗中；

支持Unity内置的Instance实现，一次DrawCall即可完成大量动画的绘制；

## 使用方法

### 数据生成

1. 给制作好的骨骼动画预制体添加脚本```GPUSkinningGenerator```，设置好动画名称和根骨骼对象，勾选创建预制体这样可以直接创建好可直接使用的GPUSkinning预制体对象，否则需要自己创建并将对应的数据进行赋值。

2. 将该预制体拖入场景中；

3. 点击启动编辑器；

4. 启动后点击开始采样；

5. 选择要输出的路径；

## 使用GPUSkinning动画

1. 选择创建好的材质球，反射贴图没有赋值，这里将原来动画的贴图赋值给新的材质球；

2. 通过代码获取对应的```GPUSkinningAnimation```，调用该对象的```Play```或者```CrossFade```方法播放对应的动画即可；

## 示例

```Assets/Sample/Test.cs```是测试代码，创建了100个GPUSkinning对象，同时开启了Instance；
